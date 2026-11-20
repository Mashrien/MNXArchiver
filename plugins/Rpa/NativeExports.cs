using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Mash.GUI.MNXArchiver.Plugins.Rpa;

internal static unsafe class NativeExports {
    private static readonly PluginFormat Format = new PluginFormat(
        "renpy.rpa", "Ren'Py RPA", "1.0.3", "2.0.0", "2.1.999",
        [".rpa"], [new PluginFormatVersion("3.0", "5250412D332E3020",
            PluginActions.Extract | PluginActions.Add | PluginActions.Replace | PluginActions.Create
                | PluginActions.Delete | PluginActions.ReadEntry | PluginActions.StreamEntry)], "3.0"
        );

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_abi_version", CallConvs = [typeof(CallConvCdecl)])]
    public static uint GetAbiVersion() {
        return PluginProtocol.AbiVersion;
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_request", CallConvs = [typeof(CallConvCdecl)])]
    public static int Request(byte* request, int requestLength, nint cancelCallback, nint state,
        byte** response, int* responseLength) {
        return HandleRequest(request, requestLength, new RpaCancellation(cancelCallback, state),
            response, responseLength);
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_read_entry", CallConvs = [typeof(CallConvCdecl)])]
    public static int ReadEntry(byte* request, int requestLength, nint cancelCallback, nint state,
        byte* destination, int destinationLength, byte** response, int* responseLength) {
        // The caller owns this buffer. The stream wrapper never retains or frees it.
        if (destinationLength < 0 || destinationLength > PluginProtocol.MaxPreviewBytes) {
            return 1;
            }
        if (destinationLength > 0 && destination == null) {
            return 1;
            }

        try {
            using Stream output = destinationLength == 0
                ? Stream.Null
                : new UnmanagedMemoryStream(destination, 0, destinationLength, FileAccess.Write);
            return HandleRequest(request, requestLength, new RpaCancellation(cancelCallback, state),
                response, responseLength, output, destinationLength);
            }
        catch (Exception) {
            return 1;
            }
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_open_entry_stream", CallConvs = [typeof(CallConvCdecl)])]
    public static int OpenEntryStream(byte* request, int requestLength, nint cancelCallback, nint state,
        nint* stream, byte** response, int* responseLength) {
        if (stream == null) {
            return 1;
            }
        *stream = 0;

        nint opened = 0;
        int status = HandleRequest(request, requestLength, new RpaCancellation(cancelCallback, state),
            response, responseLength, entryStream: &opened);
        if (status == 0) {
            *stream = opened;
            }
        else {
            DisposeEntryStream(opened);
            }
        return status;
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_read_entry_at", CallConvs = [typeof(CallConvCdecl)])]
    public static int ReadEntryAt(nint stream, long offset, byte* destination, int destinationLength,
        nint cancelCallback, nint state, int* bytesRead) {
        if (bytesRead == null) {
            return PluginProtocol.EntryStreamReadFailed;
            }
        *bytesRead = 0;
        if (stream == 0 || offset < 0 || destinationLength < 0 || (destination == null && destinationLength != 0)) {
            return PluginProtocol.EntryStreamReadFailed;
            }

        try {
            RpaEntryStream entry = (RpaEntryStream)GCHandle.FromIntPtr(stream).Target!;
            RpaCancellation cancellation = new RpaCancellation(cancelCallback, state);
            *bytesRead = entry.ReadAt(offset, new Span<byte>(destination, destinationLength), cancellation);
            return 0;
            }
        catch (OperationCanceledException) {
            return PluginProtocol.EntryStreamReadCancelled;
            }
        catch (Exception) {
            return PluginProtocol.EntryStreamReadFailed;
            }
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_close_entry_stream", CallConvs = [typeof(CallConvCdecl)])]
    public static void CloseEntryStream(nint stream) {
        DisposeEntryStream(stream);
        }

    private static void DisposeEntryStream(nint stream) {
        if (stream == 0) {
            return;
            }

        try {
            GCHandle handle = GCHandle.FromIntPtr(stream);
            try {
                if (handle.Target is RpaEntryStream entry) {
                    entry.Dispose();
                    }
                }
            finally {
                handle.Free();
                }
            }
        catch (Exception) {
            // Native callers cannot receive a managed exception on close.
            }
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_release", CallConvs = [typeof(CallConvCdecl)])]
    public static void Release(byte* response) {
        NativeMemory.Free(response);
        }

    private static int HandleRequest(byte* request, int requestLength, RpaCancellation cancellation,
        byte** response, int* responseLength, Stream? output = null, int destinationLength = 0,
        nint* entryStream = null) {
        if (response == null || responseLength == null) {
            return 1;
            }
        *response = null;
        *responseLength = 0;
        if (request == null || requestLength <= 0 || requestLength > PluginProtocol.MaxMessageBytes) {
            return 1;
            }

        try {
            PluginResponse result = ExecuteRequest(new ReadOnlySpan<byte>(request, requestLength),
                cancellation, output, destinationLength, entryStream);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(result, PluginJson.Default.PluginResponse);
            if (payload.Length > PluginProtocol.MaxMessageBytes) {
                PluginResponse tooLarge = new PluginResponse { Error = "The RPA response exceeds the plugin message limit." };
                payload = JsonSerializer.SerializeToUtf8Bytes(tooLarge, PluginJson.Default.PluginResponse);
                }

            byte* allocated = (byte*)NativeMemory.Alloc((nuint)payload.Length);
            payload.AsSpan().CopyTo(new Span<byte>(allocated, payload.Length));
            *response = allocated;
            *responseLength = payload.Length;
            return 0;
            }
        catch (Exception) {
            return 1;
            }
        }

    private static PluginResponse ExecuteRequest(ReadOnlySpan<byte> json, RpaCancellation cancellation,
        Stream? output, int destinationLength, nint* entryStream) {
        try {
            PluginRequest request = JsonSerializer.Deserialize(json, PluginJson.Default.PluginRequest)
                ?? throw new InvalidDataException("The plugin request must be a JSON object.");

            if (entryStream != null) {
                if (request.Operation != "openEntryStream") {
                    throw new InvalidDataException("This export only accepts entry stream requests.");
                    }
                CheckPeerVersions(request);
                PluginResponse result = new PluginResponse();
                RpaEntryStream stream = RpaOperations.OpenEntryStream(request, cancellation);
                try {
                    *entryStream = GCHandle.ToIntPtr(GCHandle.Alloc(stream));
                    }
                catch {
                    stream.Dispose();
                    throw;
                    }
                return result;
                }

            if (output is not null) {
                if (request.Operation != "read") {
                    throw new InvalidDataException("This export only accepts entry read requests.");
                    }
                CheckPeerVersions(request);
                RpaOperations.ReadEntry(request, output, destinationLength, cancellation);
                return new PluginResponse { BytesRead = destinationLength };
                }

            if (request.Operation == "describe") {
                return new PluginResponse { Format = Format };
                }
            CheckPeerVersions(request);
            cancellation.Check();
            if (request.Operation == "negotiate") {
                return new PluginResponse();
                }

            return RpaOperations.Run(request, cancellation);
            }
        catch (OperationCanceledException) {
            return new PluginResponse { Cancelled = true };
            }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException
            or InvalidDataException or JsonException or OverflowException or NotSupportedException) {
            return new PluginResponse { Error = error.Message };
            }
        catch (Exception) {
            return new PluginResponse { Error = "The RPA plugin could not complete the request." };
            }
        }

    private static void CheckPeerVersions(PluginRequest request) {
        if (!PluginProtocol.Supports(request.HostVersion, Format.MinHostVersion, Format.MaxHostVersion)) {
            throw new InvalidDataException("This host release is outside the supported RPA module range.");
            }
        if (!PluginProtocol.Supports(Format.Version, request.MinPluginVersion, request.MaxPluginVersion)) {
            throw new InvalidDataException("This RPA module release is outside the supported host range.");
            }
        }
    }

internal readonly unsafe struct RpaCancellation {
    private readonly nint _callback;
    private readonly nint _state;

    internal RpaCancellation(nint callback, nint state) {
        _callback = callback;
        _state = state;
        }

    internal void Check() {
        if (_callback == 0) {
            return;
            }
        delegate* unmanaged[Cdecl]<nint, int> isCancelled = (delegate* unmanaged[Cdecl]<nint, int>)_callback;
        if (isCancelled(_state) != 0) {
            throw new OperationCanceledException();
            }
        }
    }
