#include "pch.h"
#include "api_context.hpp"
#include "dllmain.hpp"

ApiResult ApiContext::LoadPrequeryBarriersIntersectionState(const seg2i16* barriers, int numBarriers, OUT uint64_t& handle) {
   auto state = ::LoadPrequeryBarriersIntersectionState(barriers, numBarriers);

   std::lock_guard<std::mutex> lock(sync);
   handle = this->nextHandle++;
   this->handleToPrequeryState[handle] = state;

   return ApiResult::Success;
}

ApiResult ApiContext::AnyIntersections(uint64_t prequeryStateHandle, const seg2i16* queries, int numQueries, uint8_t* results) {
   std::unique_lock<std::mutex> lock(sync);

   auto it = handleToPrequeryState.find(prequeryStateHandle);
   if (it == handleToPrequeryState.end()) {
      return ApiResult::ErrorUnknownHandle;
   }

   auto state = it->second;
   lock.unlock();

   ::QueryAnyIntersections(state, queries, numQueries, results);
   return ApiResult::Success;
}

ApiResult ApiContext::FreePrequeryAnySegmentIntersections(uint64_t prequeryStateHandle) {
   std::lock_guard<std::mutex> lock(sync);
   return handleToPrequeryState.erase(prequeryStateHandle) > 0
      ? ApiResult::Success
      : ApiResult::ErrorUnknownHandle;
}
