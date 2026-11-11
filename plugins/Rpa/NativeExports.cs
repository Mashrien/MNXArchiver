using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Mash.UI.MNXFile.GUI.Plugins.Rpa;

internal static unsafe class NativeExports {
    private static readonly PluginFormat Format = new("renpy.rpa", "Ren'Py RPA", "1.0.0", "2.0.0", "2.0.999",
        [".rpa"], [new PluginFormatVersion("3.0", "5250412D332E3020",
            PluginActions.Extract | PluginActions.Add | PluginActions.Replace | PluginActions.Create
                | PluginActions.Delete)], "3.0");

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_abi_version", CallConvs = [typeof(CallConvCdecl)])]
    public static uint GetAbiVersion() {
        return PluginProtocol.AbiVersion;
        }

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_request", CallConvs = [typeof(CallConvCdecl)])]
    public static int Request(byte* request, int requestLength, nint cancelCallback, nint state,
        byte** response, int* responseLength) {
        if (response == null || responseLength == null)
            return 1;
        *response = null;
        *responseLength = 0;

        if (request == null || requestLength <= 0 || requestLength > PluginProtocol.MaxMessageBytes)
            return 1;

        try {
            PluginResponse result;
            try {
                PluginRequest operation = JsonSerializer.Deserialize(
                    new ReadOnlySpan<byte>(request, requestLength), PluginJson.Default.PluginRequest)
                    ?? throw new InvalidDataException("The plugin request must be a JSON object.");
                result = Run(operation, new RpaCancellation(cancelCallback, state));
                }
            catch (OperationCanceledException) {
                result = new PluginResponse { Cancelled = true };
                }
            catch (Exception exception) when (exception is IOException or ArgumentException or
                UnauthorizedAccessException or InvalidDataException or JsonException or OverflowException or
                NotSupportedException) {
                result = new PluginResponse { Error = exception.Message };
                }
            catch (Exception) {
                result = new PluginResponse { Error = "The RPA plugin could not complete the request." };
                }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(result, PluginJson.Default.PluginResponse);
            if (payload.Length > PluginProtocol.MaxMessageBytes)
                payload = JsonSerializer.SerializeToUtf8Bytes(
                    new PluginResponse { Error = "The RPA response exceeds the plugin message limit." },
                    PluginJson.Default.PluginResponse);

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

    [UnmanagedCallersOnly(EntryPoint = "mnx_plugin_release", CallConvs = [typeof(CallConvCdecl)])]
    public static void Release(byte* response) {
        NativeMemory.Free(response);
        }

    private static PluginResponse Run(PluginRequest request, RpaCancellation cancellation) {
        if (request.Operation == "describe")
            return new PluginResponse { Format = Format };

        if (!PluginProtocol.Supports(request.HostVersion, Format.MinHostVersion, Format.MaxHostVersion)
            || !PluginProtocol.Supports(Format.Version, request.MinPluginVersion, request.MaxPluginVersion))
            throw new InvalidDataException("The host and RPA plugin versions are not mutually compatible.");

        cancellation.Check();
        if (request.Operation == "negotiate")
            return new PluginResponse();

        return RpaOperations.Run(request, cancellation);
        }
    }

internal readonly unsafe struct RpaCancellation(nint callback, nint state) {
    internal void Check() {
        if (callback != 0 && ((delegate* unmanaged[Cdecl]<nint, int>)callback)(state) != 0)
            throw new OperationCanceledException();
        }
    }
