// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

#define IN
#define OUT
#define FORCE_INLINE __forceinline

#define OPAQUE_HANDLE void*

enum class ApiResult : int {
   Success = 0,
   ErrorUnknownHandle = -100,
   ErrorUnknown = -999
};

#define DECLARE_API(Name) ApiResult __declspec(dllexport) NativeApi_##Name
#define IMPLEMENT_API(Name) ApiResult NativeApi_##Name

#endif //PCH_H
