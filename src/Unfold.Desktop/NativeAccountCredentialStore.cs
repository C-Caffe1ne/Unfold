using System.Runtime.InteropServices;

namespace Unfold.Desktop;

/// <summary>Per-OS-user storage, scoped to this Supabase project and data profile. No plain-text fallback.</summary>
internal sealed class NativeAccountCredentialStore(string scope) : IAccountCredentialStore
{
    public byte[]? Read() => OperatingSystem.IsMacOS() ? Mac.Read(scope)
        : OperatingSystem.IsWindows() ? Windows.Read(scope) : throw new PlatformNotSupportedException();
    public void Write(byte[] value)
    {
        if (OperatingSystem.IsMacOS()) Mac.Write(scope, value);
        else if (OperatingSystem.IsWindows()) Windows.Write(scope, value);
        else throw new PlatformNotSupportedException();
    }
    public void Delete()
    {
        if (OperatingSystem.IsMacOS()) Mac.Delete(scope);
        else if (OperatingSystem.IsWindows()) Windows.Delete(scope);
        else throw new PlatformNotSupportedException();
    }

    private static class Windows
    {
        private const uint Generic = 1, LocalMachine = 2;
        private const int NotFound = 1168;
        private static string Target(string scope) => "DokhuStudio/Unfold/session/" + scope;
        internal static byte[]? Read(string scope)
        {
            if (!CredRead(Target(scope), Generic, 0, out var pointer))
            {
                if (Marshal.GetLastWin32Error() == NotFound) return null;
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            try
            {
                var credential = Marshal.PtrToStructure<Credential>(pointer);
                if (credential.BlobSize > 2560) throw new IOException("Invalid stored credential size.");
                var bytes = new byte[credential.BlobSize];
                if (bytes.Length > 0) Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
                return bytes;
            }
            finally { CredFree(pointer); }
        }
        internal static unsafe void Write(string scope, byte[] value)
        {
            fixed (byte* pointer = value)
            {
                var credential = new Credential
                {
                    Type = Generic, TargetName = Target(scope), UserName = "Unfold",
                    BlobSize = (uint)value.Length, Blob = (IntPtr)pointer, Persist = LocalMachine
                };
                if (!CredWrite(ref credential, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        internal static void Delete(string scope)
        {
            if (!CredDelete(Target(scope), Generic, 0) && Marshal.GetLastWin32Error() != NotFound)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public uint Flags, Type;
            public string? TargetName, Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint BlobSize;
            public IntPtr Blob;
            public uint Persist, AttributeCount;
            public IntPtr Attributes;
            public string? TargetAlias, UserName;
        }
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential, uint flags);
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target, uint type, uint flags);
        [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr pointer);
    }

    private static class Mac
    {
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string Core = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private static readonly IntPtr SecurityLibrary = NativeLibrary.Load(Security);
        private static readonly IntPtr CoreLibrary = NativeLibrary.Load(Core);
        private const int NotFound = -25300;
        private static IntPtr Constant(string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(SecurityLibrary, name));
        private static IntPtr True => Marshal.ReadIntPtr(NativeLibrary.GetExport(CoreLibrary, "kCFBooleanTrue"));
        private static void Check(int status) { if (status != 0) throw new IOException($"Keychain operation failed ({status})."); }

        internal static byte[]? Read(string scope)
        {
            using var query = Query(scope);
            query.Set("kSecReturnData", True); query.Set("kSecMatchLimit", Constant("kSecMatchLimitOne"));
            var status = SecItemCopyMatching(query.Pointer, out var data);
            if (status == NotFound) return null;
            Check(status);
            try
            {
                var length = CFDataGetLength(data);
                if (length is < 0 or > 2560) throw new IOException("Invalid stored credential size.");
                var bytes = new byte[(int)length];
                if (bytes.Length > 0) Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, bytes.Length);
                return bytes;
            }
            finally { CFRelease(data); }
        }
        internal static void Write(string scope, byte[] value)
        {
            using var query = Query(scope);
            using var update = new Dictionary();
            var data = CFDataCreate(IntPtr.Zero, value, value.Length);
            if (data == IntPtr.Zero) throw new IOException("Cannot allocate credential data.");
            try
            {
                update.Set("kSecValueData", data);
                var status = SecItemUpdate(query.Pointer, update.Pointer);
                if (status == NotFound) { query.Set("kSecValueData", data); status = SecItemAdd(query.Pointer, IntPtr.Zero); }
                Check(status);
            }
            finally { CFRelease(data); }
        }
        internal static void Delete(string scope)
        {
            using var query = Query(scope);
            var status = SecItemDelete(query.Pointer);
            if (status != NotFound) Check(status);
        }
        private static Dictionary Query(string scope)
        {
            var query = new Dictionary();
            query.Set("kSecClass", Constant("kSecClassGenericPassword"));
            query.Text("kSecAttrService", "com.dokhustudio.Unfold.session");
            query.Text("kSecAttrAccount", scope);
            return query;
        }
        private sealed class Dictionary : IDisposable
        {
            internal IntPtr Pointer { get; } = CFDictionaryCreateMutable(IntPtr.Zero, 0,
                NativeLibrary.GetExport(CoreLibrary, "kCFTypeDictionaryKeyCallBacks"),
                NativeLibrary.GetExport(CoreLibrary, "kCFTypeDictionaryValueCallBacks"));
            internal void Set(string key, IntPtr value) => CFDictionarySetValue(Pointer, Constant(key), value);
            internal void Text(string key, string value)
            {
                var text = CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
                if (text == IntPtr.Zero) throw new IOException("Cannot allocate credential attribute.");
                try { Set(key, text); } finally { CFRelease(text); }
            }
            public void Dispose() => CFRelease(Pointer);
        }
        [DllImport(Security)] private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
        [DllImport(Security)] private static extern int SecItemUpdate(IntPtr query, IntPtr attributes);
        [DllImport(Security)] private static extern int SecItemAdd(IntPtr attributes, IntPtr result);
        [DllImport(Security)] private static extern int SecItemDelete(IntPtr query);
        [DllImport(Core)] private static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, nint capacity, IntPtr keys, IntPtr values);
        [DllImport(Core)] private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);
        [DllImport(Core)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, uint encoding);
        [DllImport(Core)] private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);
        [DllImport(Core)] private static extern nint CFDataGetLength(IntPtr data);
        [DllImport(Core)] private static extern IntPtr CFDataGetBytePtr(IntPtr data);
        [DllImport(Core)] private static extern void CFRelease(IntPtr value);
    }
}
