#pragma once

#include "pch.h"
#include <unordered_map>
#include "dllmain.hpp"

struct seg2i16;

class ApiContext {
   std::mutex sync;
   std::unordered_map<uint64_t, std::shared_ptr<Avx2IntersectionPrequeryState>> handleToPrequeryState;
   uint64_t nextHandle = 1;

public:
   ApiResult LoadPrequeryBarriersIntersectionState(const seg2i16* barriers, int numBarriers, OUT uint64_t& handle);
   ApiResult AnyIntersections(uint64_t prequeryStateHandle, const seg2i16* queries, int numQueries, uint8_t* results);
   ApiResult FreePrequeryAnySegmentIntersections(uint64_t prequeryStateHandle);
};
