#include "../../plugins/include/mnx_plugin.h"
#include <stdlib.h>
#include <string.h>

#ifndef RELEASE
#define RELEASE "1.0.0"
#endif
#ifndef MIN_HOST
#define MIN_HOST "2.0.0"
#endif
#ifndef ABI
#define ABI MNX_PLUGIN_ABI_VERSION
#endif

uint32_t mnx_plugin_abi_version(void) {
    return ABI;
}

int32_t mnx_plugin_request(const uint8_t *request, int32_t length,
    mnx_plugin_cancel cancel, void *state, uint8_t **response, int32_t *response_length) {
    (void)cancel;
    (void)state;
    const char *description = "{\"format\":{\"id\":\"test.rejected\",\"name\":\"Test module\","
        "\"version\":\"" RELEASE "\",\"minHostVersion\":\"" MIN_HOST "\",\"maxHostVersion\":\"2.9.999\","
        "\"extensions\":[\".test\"],\"versions\":[{\"version\":\"1.0\",\"magicHex\":\"74657374\",\"actions\":0}],"
        "\"currentVersion\":\"1.0\"}}";
    const char *json = "{\"error\":\"Fixture refused negotiation\"}";
    const char *describe = "\"describe\"";
    size_t describe_length = strlen(describe);
    for (int32_t offset = 0; offset <= length - (int32_t)describe_length; ++offset) {
        if (memcmp(request + offset, describe, describe_length) == 0) {
            json = description;
            break;
        }
    }

    *response_length = (int32_t)strlen(json);
    *response = (uint8_t *)malloc((size_t)*response_length);
    if (*response == NULL)
        return 1;
    memcpy(*response, json, (size_t)*response_length);
    return 0;
}

void mnx_plugin_release(uint8_t *response) {
    free(response);
}
