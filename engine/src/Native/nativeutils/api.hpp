#pragma once
#include "pch.h"

struct seg2i16;

extern "C" {
   DECLARE_API(GetVersion)(OUT int& version);
   DECLARE_API(LoadPrequeryAnySegmentIntersections)(const seg2i16* barriers, int numBarriers, OUT OPAQUE_HANDLE& handle);
   DECLARE_API(QueryAnySegmentIntersections)(OPAQUE_HANDLE prequeryStateHandle, const seg2i16* queries, int numQueries, uint8_t* results);
   DECLARE_API(FreePrequeryAnySegmentIntersections)(OPAQUE_HANDLE prequeryStateHandle);
}