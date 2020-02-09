#pragma once

struct seg2i16;

typedef struct Avx2IntersectionPrequeryState_s {
   int NumChunks;
   std::shared_ptr<char> ChunkBuffer;
} Avx2IntersectionPrequeryState;

std::shared_ptr<Avx2IntersectionPrequeryState> LoadPrequeryBarriersIntersectionState(const seg2i16* barriers, int numBarriers);
void QueryAnyIntersections(std::shared_ptr<Avx2IntersectionPrequeryState> state, const seg2i16* queries, int numQueries, uint8_t* results);
