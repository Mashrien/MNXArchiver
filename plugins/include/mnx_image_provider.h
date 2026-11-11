#ifndef MNX_IMAGE_PROVIDER_H
#define MNX_IMAGE_PROVIDER_H

#include "mnx_native_extension.h"

#define MNX_IMAGE_PROVIDER_ABI_VERSION 1u
#define MNX_IMAGE_PROVIDER_MAX_DESCRIPTION_BYTES (16 * 1024)
#define MNX_IMAGE_PROVIDER_MAX_DIMENSION 16384
#define MNX_IMAGE_PROVIDER_MAX_FRAMES 1024

#define MNX_IMAGE_SUCCESS 0
#define MNX_IMAGE_INVALID_IMAGE 1
#define MNX_IMAGE_ANIMATED_IMAGE 2
#define MNX_IMAGE_INVALID_DESTINATION 3
#define MNX_IMAGE_DECODE_FAILED 4

#ifdef __cplusplus
extern "C" {
#endif

MNX_PLUGIN_EXPORT uint32_t MNX_PLUGIN_CALL mnx_image_provider_abi_version(void);

/* Return the UTF-8 JSON descriptor length. With NULL and capacity 0, return
 * the required length. Write exactly that many bytes if capacity permits. */
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_image_provider_describe(
    uint8_t *destination, int32_t capacity);

typedef struct mnx_image_info {
    int32_t width;
    int32_t height;
    int32_t frame_count;
    int32_t total_plays; /* 0 repeats forever. Use 1 for a still image. */
} mnx_image_info;

/* Read source dimensions and playback metadata without decoding pixels. */
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_image_provider_inspect(
    const uint8_t *source, int32_t source_length, mnx_image_info *image);

/* Decode each composited frame to top-down, sRGB, premultiplied BGRA8.
 * Dimensions are original or half-size rounded up. Stride is in bytes.
 * The host owns frame_count buffers, each of frame_length bytes, and
 * frame_count delay entries in milliseconds. Write delay 0 for a still.
 * Do not retain any pointers. */
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_image_provider_decode(
    const uint8_t *source, int32_t source_length,
    uint8_t *const *frames, int64_t frame_length,
    int32_t width, int32_t height, int32_t stride,
    int32_t frame_count, int32_t *delays);

#ifdef __cplusplus
}
#endif
#endif
