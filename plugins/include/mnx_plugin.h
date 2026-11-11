#ifndef MNX_PLUGIN_H
#define MNX_PLUGIN_H

#include <stdint.h>

#if defined(_WIN32)
#define MNX_PLUGIN_CALL __cdecl
#if defined(MNX_PLUGIN_BUILD)
#define MNX_PLUGIN_EXPORT __declspec(dllexport)
#else
#define MNX_PLUGIN_EXPORT
#endif
#else
#define MNX_PLUGIN_CALL
#define MNX_PLUGIN_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define MNX_PLUGIN_ABI_VERSION 1u
#define MNX_PLUGIN_MAX_MESSAGE_BYTES (64 * 1024 * 1024)
#define MNX_PLUGIN_EXTRACT (1u << 0)
#define MNX_PLUGIN_ADD (1u << 1)
#define MNX_PLUGIN_REPLACE (1u << 2)
#define MNX_PLUGIN_CREATE (1u << 3)
#define MNX_PLUGIN_DELETE (1u << 4)

typedef int32_t (MNX_PLUGIN_CALL *mnx_plugin_cancel)(void *state);

MNX_PLUGIN_EXPORT uint32_t MNX_PLUGIN_CALL mnx_plugin_abi_version(void);
/* UTF-8 JSON, explicit byte lengths (no terminating NUL required).
 * Return 0 with a response for application errors or cancellation as well.
 * Transfer response allocation to caller; caller releases it exactly once.
 * Do not retain request, callback, or state after returning.
 */
MNX_PLUGIN_EXPORT int32_t MNX_PLUGIN_CALL mnx_plugin_request(
    const uint8_t *request, int32_t request_length,
    mnx_plugin_cancel cancel, void *state,
    uint8_t **response, int32_t *response_length);
MNX_PLUGIN_EXPORT void MNX_PLUGIN_CALL mnx_plugin_release(uint8_t *response);

#ifdef __cplusplus
}
#endif
#endif
