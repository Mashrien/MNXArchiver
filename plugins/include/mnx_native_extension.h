#ifndef MNX_NATIVE_EXTENSION_H
#define MNX_NATIVE_EXTENSION_H

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

#endif
