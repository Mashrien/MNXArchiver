#ifndef MNX_VIDEO_PROVIDER_H
#define MNX_VIDEO_PROVIDER_H

#include "mnx_native_extension.h"

#define MNX_VIDEO_PROVIDER_ABI_VERSION 1u
#define MNX_VIDEO_HALF_RESOLUTION 1u

#define MNX_VIDEO_SUCCESS 0
#define MNX_VIDEO_END_OF_STREAM 1
#define MNX_VIDEO_INVALID_MEDIA 2
#define MNX_VIDEO_MISSING_DEPENDENCY 3
#define MNX_VIDEO_UNSUPPORTED 4
#define MNX_VIDEO_CANCELLED 5
#define MNX_VIDEO_FAILED 6

#define MNX_VIDEO_SEEK_SIZE 0x10000
#define MNX_VIDEO_SEEK_FORCE 0x20000
#define MNX_VIDEO_READ_EOF (-541478725)
#define MNX_VIDEO_READ_CANCELLED (-1414092869)

#ifdef __cplusplus
extern "C" {
#endif

/* Callback state and function addresses remain valid until close returns.
 * Read fills up to capacity bytes and returns the byte count, EOF or an
 * AVERROR. Seek returns the resulting offset or a negative error. A size
 * query returns the length without moving the stream. */
typedef struct mnx_video_input {
    void *state;
    int32_t (MNX_PLUGIN_CALL *read)(void *state, uint8_t *bytes, int32_t capacity);
    int64_t (MNX_PLUGIN_CALL *seek)(void *state, int64_t offset, int32_t whence);
    int32_t (MNX_PLUGIN_CALL *cancelled)(void *state);
} mnx_video_input;

typedef struct mnx_video_info {
    int32_t width;
    int32_t height;
    int32_t fps_numerator;
    int32_t fps_denominator;
    int64_t duration_ms; /* -1 when unknown */
} mnx_video_info;

MNX_PLUGIN_EXPORT uint32_t MNX_PLUGIN_CALL mnx_video_provider_abi_version(void);
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_video_provider_describe(uint8_t *destination, int32_t capacity);

/* The provider copies the input table. Calls for each handle are serialized.
 * Validate container bytes during open before passing them to a demuxer. */
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_video_provider_open_stream(
    const mnx_video_input *input, uint32_t flags, void **handle);
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_video_provider_get_info(void *handle, mnx_video_info *info);

/* Writes one top-down opaque BGRA8 frame directly into caller-owned memory.
 * Never retains bgra or pts_ms. Timestamps are relative to the video start. */
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_video_provider_decode_next_frame(
    void *handle, uint8_t *bgra, int64_t capacity, int32_t stride, int64_t *pts_ms);
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_video_provider_seek_ms(void *handle, int64_t target_ms);
MNX_PLUGIN_EXPORT void MNX_PLUGIN_CALL mnx_video_provider_close(void *handle);

#ifdef __cplusplus
}
#endif
#endif
