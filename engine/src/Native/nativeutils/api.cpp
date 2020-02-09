#include "pch.h"
#include "api.hpp"
#include "api_context.hpp"

namespace {
   std::shared_ptr<ApiContext> context = std::make_shared<ApiContext>();
}

#define ERROR_WRAPPER_BEGIN \
      try {

#define ERROR_WRAPPER_END \
      } catch(const std::exception& e) { \
         std::cout << "NATIVE ERROR @ " << __FILE__ << " Line " << __LINE__ << ": " << e.what() << std::endl; \
         return ApiResult::ErrorUnknown; \
      }

IMPLEMENT_API(GetVersion)(OUT int& version) {
   ERROR_WRAPPER_BEGIN
   version = 1337;
   return ApiResult::Success;
   ERROR_WRAPPER_END
}

IMPLEMENT_API(LoadPrequeryAnySegmentIntersections)(const seg2i16* barriers, int numBarriers, OUT OPAQUE_HANDLE& handle) {
   ERROR_WRAPPER_BEGIN
   return context->LoadPrequeryBarriersIntersectionState(barriers, numBarriers, OUT reinterpret_cast<uint64_t&>(handle));
   ERROR_WRAPPER_END
}

IMPLEMENT_API(QueryAnySegmentIntersections)(OPAQUE_HANDLE prequeryStateHandle, const seg2i16* queries, int numQueries, uint8_t* results) {
   ERROR_WRAPPER_BEGIN
   return context->AnyIntersections(reinterpret_cast<uint64_t>(prequeryStateHandle), queries, numQueries, results);
   ERROR_WRAPPER_END
}

IMPLEMENT_API(FreePrequeryAnySegmentIntersections)(OPAQUE_HANDLE prequeryStateHandle) {
   ERROR_WRAPPER_BEGIN
   return context->FreePrequeryAnySegmentIntersections(reinterpret_cast<uint64_t>(prequeryStateHandle));
   ERROR_WRAPPER_END
}