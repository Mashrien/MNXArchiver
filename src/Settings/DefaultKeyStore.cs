using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DiskFile = System.IO.File;

namespace Mash.UI.MNXFile.GUI.Settings;

/// <summary>AES-GCM password envelope. The wrapping key lives in the OS credential store.</summary>
internal sealed class DefaultKeyStore {
    private readonly AppSettings _settings;
    private static readonly byte[] Magic = "MNXKEY01"u8.ToArray();
    private readonly string _identity;
    internal DefaultKeyStore(AppSettings settings) {
        _settings = settings;
        _identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(settings.KeyPath))));
        }
    internal bool Exists => DiskFile.Exists(_settings.KeyPath);

    internal void Store(string password) {
        if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 16384) {
            throw new ArgumentException("Enter a password of 1–16,384 UTF-8 bytes.");
            }
        _settings.EnsurePrivateDirectory();
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] clear = Encoding.UTF8.GetBytes(password);
        byte[] protectedKey = [];
        byte[] envelope = [];
        try {
            if (OperatingSystem.IsWindows()) {
                protectedKey = ProtectWindows(key, false);
                }
            else {
                string? existing = RunSecretTool("lookup", null);
                if (!string.IsNullOrWhiteSpace(existing)) {
                    CryptographicOperations.ZeroMemory(key);
                    key = Convert.FromHexString(existing.Trim());
                    }
                else {
                    RunSecretTool("store", Convert.ToHexString(key));
                    }
                }
            envelope = Encrypt(clear, key, protectedKey);
            AppSettings.WritePrivate(_settings.KeyPath, envelope);
            }
        finally {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(clear);
            }
        }

    internal string Read() {
        if (new FileInfo(_settings.KeyPath).Length > 65536) {
            throw new CryptographicException("The saved MNX default key file is too large.");
            }
        byte[] envelope = DiskFile.ReadAllBytes(_settings.KeyPath);
        if (envelope.Length > 65536 || envelope.Length < 41 || !envelope.AsSpan(0, 8).SequenceEqual(Magic)) {
            throw new CryptographicException("The saved MNX default key file is invalid.");
            }
        int protectedLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(envelope.AsSpan(8, 4));
        if (protectedLength < 0 || protectedLength > envelope.Length - 40) {
            throw new CryptographicException("The saved key envelope is invalid.");
            }
        byte[] key;
        if (OperatingSystem.IsWindows()) {
            key = ProtectWindows(envelope.AsSpan(12, protectedLength).ToArray(), true);
            }
        else {
            string? value = RunSecretTool("lookup", null);
            if (string.IsNullOrWhiteSpace(value)) {
                throw new CryptographicException("The desktop keyring does not contain the key for this default password.");
                }
            key = Convert.FromHexString(value.Trim());
            }
        byte[] clear = [];
        try {
            clear = Decrypt(envelope, key);
            return Encoding.UTF8.GetString(clear);
            }
        finally {
            CryptographicOperations.ZeroMemory(clear);
            CryptographicOperations.ZeroMemory(key);
            }
        }

    internal void Clear() {
        // Delete the ciphertext even when the keyring is currently unavailable.
        if (Exists) {
            if (DiskFile.GetAttributes(_settings.KeyPath).HasFlag(FileAttributes.ReparsePoint)) {
                throw new IOException("Refusing to overwrite a saved-key path that is a symbolic link.");
                }
            using (FileStream output = new FileStream(_settings.KeyPath, FileMode.Open, FileAccess.Write, FileShare.None)) {
                byte[] random = new byte[4096];
                long remaining = output.Length;
                while (remaining > 0) {
                    RandomNumberGenerator.Fill(random);
                    int count = (int)Math.Min(remaining, random.Length);
                    output.Write(random, 0, count);
                    remaining -= count;
                    }
                output.Flush(true);
                }
            DiskFile.Delete(_settings.KeyPath);
            }
        if (!OperatingSystem.IsWindows()) {
            RunSecretTool("clear", null);
            }
        }

    internal static byte[] Encrypt(byte[] clear, byte[] key, byte[] protectedKey) {
        byte[] envelope = new byte[checked(12 + protectedKey.Length + 12 + 16 + clear.Length)];
        Magic.CopyTo(envelope, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(8, 4), protectedKey.Length);
        protectedKey.CopyTo(envelope, 12);
        int start = 12 + protectedKey.Length;
        RandomNumberGenerator.Fill(envelope.AsSpan(start, 12));
        using AesGcm cipher = new AesGcm(key, 16);
        cipher.Encrypt(envelope.AsSpan(start, 12), clear, envelope.AsSpan(start + 28), envelope.AsSpan(start + 12, 16), envelope.AsSpan(0, start));
        return envelope;
        }

    internal static byte[] Decrypt(byte[] envelope, byte[] key) {
        int start = checked(12 + System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(envelope.AsSpan(8, 4)));
        if (start < 12 || start > envelope.Length - 28 || !envelope.AsSpan(0, 8).SequenceEqual(Magic)) {
            throw new CryptographicException("The saved key envelope is invalid.");
            }
        byte[] clear = new byte[envelope.Length - start - 28];
        try {
            using AesGcm cipher = new AesGcm(key, 16);
            cipher.Decrypt(envelope.AsSpan(start, 12), envelope.AsSpan(start + 28), envelope.AsSpan(start + 12, 16), clear, envelope.AsSpan(0, start));
            return clear;
            }
        catch {
            CryptographicOperations.ZeroMemory(clear);
            throw;
            }
        }

    private string? RunSecretTool(string operation, string? input) {
        ProcessStartInfo command = new ProcessStartInfo("secret-tool") {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
            };
        command.ArgumentList.Add(operation);
        if (operation == "store") {
            command.ArgumentList.Add("--label=MNX Archive Manager default encryption key");
            }
        command.ArgumentList.Add("application");
        command.ArgumentList.Add("mnxarchiver");
        command.ArgumentList.Add("instance");
        command.ArgumentList.Add(_identity);
        using Process process = Process.Start(command) ?? throw new IOException("Could not start the desktop keyring helper (secret-tool).");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (input is not null) {
            process.StandardInput.Write(input);
            }
        process.StandardInput.Close();
        if (!process.WaitForExit(60000)) {
            process.Kill(true);
            process.WaitForExit();
            throw new IOException("The desktop keyring did not respond within 60 seconds.");
            }
        string result = output.GetAwaiter().GetResult();
        string diagnostic = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0 && !(operation is "lookup" or "clear" && process.ExitCode == 1 && diagnostic.Length == 0)) {
            throw new IOException("Desktop keyring unavailable. Unlock your wallet and ensure libsecret's secret-tool is installed. " + diagnostic.Trim());
            }
        return result;
        }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob {
        internal int Length;
        internal IntPtr Data;
        }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    private static byte[] ProtectWindows(byte[] bytes, bool decrypt) {
        DataBlob input = new DataBlob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        DataBlob output = default;
        try {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool success = decrypt
                ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptProtectData(ref input, "MNX default key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            byte[] result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
            }
        finally {
            for (int index = 0; index < input.Length; index++) {
                Marshal.WriteByte(input.Data, index, 0);
                }
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) {
                for (int index = 0; index < output.Length; index++) {
                    Marshal.WriteByte(output.Data, index, 0);
                    }
                LocalFree(output.Data);
                }
            }
        }
    }
