using System.Runtime.InteropServices;

namespace Mash.UI.MNXFile.GUI.Previews;

/// <summary>Wrap private decoder subclasses through their public GTK base types.
/// GtkSharp's generated casts otherwise return null for GIF/nonanimated subclasses.</summary>
internal static class ImageDecoder {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetAnimationDelegate(IntPtr loader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetIteratorDelegate(IntPtr animation, IntPtr time);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ReferenceDelegate(IntPtr instance);
    private static readonly IntPtr PixbufLibrary = NativeLibrary.Load(OperatingSystem.IsWindows() ? "libgdk_pixbuf-2.0-0.dll" : "libgdk_pixbuf-2.0.so.0");
    private static readonly IntPtr ObjectLibrary = NativeLibrary.Load(OperatingSystem.IsWindows() ? "libgobject-2.0-0.dll" : "libgobject-2.0.so.0");
    private static readonly GetAnimationDelegate GetAnimation = Marshal.GetDelegateForFunctionPointer<GetAnimationDelegate>(NativeLibrary.GetExport(PixbufLibrary, "gdk_pixbuf_loader_get_animation"));
    private static readonly GetIteratorDelegate GetIterator = Marshal.GetDelegateForFunctionPointer<GetIteratorDelegate>(NativeLibrary.GetExport(PixbufLibrary, "gdk_pixbuf_animation_get_iter"));
    private static readonly ReferenceDelegate Reference = Marshal.GetDelegateForFunctionPointer<ReferenceDelegate>(NativeLibrary.GetExport(ObjectLibrary, "g_object_ref"));

    internal static Gdk.PixbufAnimation Animation(Gdk.PixbufLoader loader) {
        IntPtr animation = GetAnimation(loader.Handle);
        if (animation == IntPtr.Zero) {
            throw new IOException("The image decoder did not return an image.");
            }
        // get_animation lends its reference; the wrapper takes its own reference.
        return new Gdk.PixbufAnimation(Reference(animation));
        }
    internal static Gdk.PixbufAnimationIter Iterator(Gdk.PixbufAnimation animation) {
        IntPtr iterator = GetIterator(animation.Handle, IntPtr.Zero);
        if (iterator == IntPtr.Zero) {
            throw new IOException("The animation decoder did not return a frame iterator.");
            }
        // get_iter transfers ownership of the returned reference.
        return new Gdk.PixbufAnimationIter(iterator);
        }
    }
