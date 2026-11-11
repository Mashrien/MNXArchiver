# Image preview providers

Native extensions use the `ext.<aspect>.<FORMAT>.mnxp` filename convention. For example,
`ext.format.RPA.mnxp` handles archive operations and `ext.image.WEBP.mnxp` decodes WebP previews.
MNXArchiver uses the exports to identify the interface supported by a library.
Each aspect can have its own interface.

Place the platform build of `ext.image.WEBP.mnxp` in the
[per-user plugins directory](ARCHIVE-PLUGINS.md) and restart MNXArchiver.
**Help → About** lists loaded image providers. Enable static image previews in
Options. The WebP provider reads static lossy and lossless images, including
alpha, and uses the SkiaSharp native library shipped with MNXArchiver.

## Implement a provider

Use [mnx_image_provider.h](../plugins/include/mnx_image_provider.h) for the
native ABI. The [WebP source](../plugins/Webp/) shows a C# NativeAOT shared
library that implements it. The host loads each image provider by its ABI
version export and verifies the descriptor before showing its extensions.

The UTF-8 JSON descriptor describes the provider and the host versions it accepts.

```json
{
  "id": "webp",
  "name": "WebP",
  "version": "1.0.1",
  "minHostVersion": "2.0.0",
  "maxHostVersion": "2.1.999",
  "extensions": [".webp"]
}
```

Use a unique ID and declare the extensions you handle. The 2.1 host accepts
module releases 1.0.0 through 1.0.999 and image provider ABI 1. Two providers
cannot claim the same extension.

1. **Inspect** the encoded bytes. Check the format signature and report
   dimensions from 1 through 16,384 pixels per axis, 1 through 1,024 frames,
   and the total number of plays. Use one play for a still image and zero
   for an infinite animation. Inspect reads metadata without decoding pixels.
2. **Decode** in one call after the GUI reserves memory. The GUI supplies one
   buffer per frame, a common stride and an array for frame delays in
   milliseconds. Write composited top-down premultiplied sRGB BGRA8 pixels at
   the original dimensions or at half resolution rounded up. Use the header’s
   status codes for malformed data, unsupported animation or an invalid buffer.

The host supplies bytes from the preview reader, whether the entry lives in
MNX or an archive module. Built-in formats take priority. Only preview
requests pass through this interface. The provider borrows the input and
output buffers for the duration of the call. Keep decoder working memory
within two full-size four-byte rasters. The GUI budgets that space along with
all retained frames, a display copy and the encoded input.

Animated providers supply fully composited frames, including any blending
and disposal specified by their image format. The GUI checks the animation
setting and decoded-memory limit before allocating frames, then replays the
cache without calling the provider again. The reference WebP module handles
still images.

Build the reference provider with the .NET 10 SDK and the NativeAOT toolchain:

```sh
dotnet publish plugins/Webp/ext.image.WEBP.csproj -c Release -r linux-x64 -o /tmp/mnx-webp
```

Use `win-x64` with the Windows NativeAOT toolchain or `linux-arm64` on an ARM64
Linux build host.
