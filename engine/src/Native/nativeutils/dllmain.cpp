// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "dllmain.hpp"
#include <cassert>

#if WINDOWS
BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
#endif

struct point2i16 {
   short x;
   short y;
};

struct seg2i16 {
   union {
      struct {
         short x1, y1, x2, y2;
      };
      struct {
         point2i16 p1, p2;
      };
   };
};

static_assert(sizeof(seg2i16) == 8, "seg2i16 must be packed");

std::vector<seg2i16> parse(const std::string& fileName) {
   std::vector<seg2i16> res;

   std::fstream fs(fileName, std::fstream::in);
   std::string line;
   while (std::getline(fs, line)) {
      std::istringstream reader(line);

      seg2i16 seg;
      char temp;
      reader >> seg.x1 >> temp >> seg.y1 >> temp >> seg.x2 >> temp >> seg.y2;

      res.emplace_back(seg);
   }

   return res;
}

template <typename T> int cmp(T v0, T v1) {
   return (v1 < v0) - (v0 < v1);
}

template <typename T> int sign(T val) {
   return cmp(val, 0);
}

int clk(short ax, short ay, short bx, short by) {
   // sign(ax * by - ay * bx);
   auto v0 = static_cast<int>(ax) * by;
   auto v1 = static_cast<int>(ay) * bx;
   return cmp(v0, v1);
}

int g_segs = 0;

bool AnyIntersections(seg2i16 query, const std::vector<seg2i16>& segments, bool detectEndpointContainment) {
   short ax = query.x1;
   short ay = query.y1;
   short bx = query.x2;
   short by = query.y2;

   short bax = bx - ax;
   short bay = by - ay;

   for (const auto& seg : segments) {
      g_segs++;
      short cx = seg.x1;
      short cy = seg.y1;
      short dx = seg.x2;
      short dy = seg.y2;

      short bcx = bx - cx;
      short bcy = by - cy;
      short bdx = bx - dx;
      short bdy = by - dy;

      auto o1 = clk(bax, bay, bcx, bcy);
      auto o2 = clk(bax, bay, bdx, bdy);
      if (o1 == o2 && !detectEndpointContainment) continue;

      short dcx = dx - cx;
      short dcy = dy - cy;
      short dax = dx - ax;
      short day = dy - ay;
      short dbx = dx - bx;
      short dby = dy - by;

      auto o3 = clk(dcx, dcy, dax, day);
      auto o4 = clk(dcx, dcy, dbx, dby);

      // std::cout
      //    << bax * bcy - bay * bcx << " "
      //    << bax * bdy - bay * bdx << " "
      //    << dcx * day - dcy * dax << " "
      //    << dcx * dby - dcy * dbx << std::endl;

      // std::cout
      //    << bax * bcy << " " << bay * bcx << " "
      //    << bax * bdy << " " << bay * bdx << " "
      //    << dcx * day << " " << dcy * dax << " "
      //    << dcx * dby << " " << dcy * dbx << std::endl;

      // bax bcy + aby bcx
      // bax bdy + aby bdx
      // cdx ady + dcy adx
      // cdx bdy + dcy bdx
      // std::cout
      //    << bax << " " << bcy << " " << -bay << " " << bcx << " "
      //    << bax << " " << bdy << " " << -bay << " " << bdx << " "
      //    << -dcx << " " << -day << " " << dcy << " " << -dax << " "
      //    << -dcx << " " << -dby << " " << dcy << " " << -dbx << std::endl;
      
      // std::cout << o1 << " " << o2 << " " << o3 << " " << o4 << std::endl;

      if (o1 != o2 && o3 != o4) return true;

      if (detectEndpointContainment) {
         throw std::exception("not implemented");
      }
   }
   return false;
}

int CountIntersections(const std::vector<seg2i16>& barriers, const std::vector<seg2i16>& queries) {
   int pass = 0;
   for (const auto& query : queries) {
      auto occluded = AnyIntersections(query, barriers, false);
      pass += !occluded;
   }

   return pass;
}

// #define EnableDebugDump

#ifndef EnableDebugDump
#define DumpI16s(reg)
#define DumpI32s(reg)
#define IfDump(x)
#else
#define DumpI16s(reg) std::cout << #reg << " : " \
   << (int16_t)_mm256_extract_epi16(reg, 0) << " " << (int16_t)_mm256_extract_epi16(reg, 1) << " " << (int16_t)_mm256_extract_epi16(reg, 2) << " " << (int16_t)_mm256_extract_epi16(reg, 3) << " " \
   << (int16_t)_mm256_extract_epi16(reg, 4) << " " << (int16_t)_mm256_extract_epi16(reg, 5) << " " << (int16_t)_mm256_extract_epi16(reg, 6) << " " << (int16_t)_mm256_extract_epi16(reg, 7) << " " \
   << (int16_t)_mm256_extract_epi16(reg, 8) << " " << (int16_t)_mm256_extract_epi16(reg, 9) << " " << (int16_t)_mm256_extract_epi16(reg, 10) << " " << (int16_t)_mm256_extract_epi16(reg, 11) << " " \
   << (int16_t)_mm256_extract_epi16(reg, 12) << " " << (int16_t)_mm256_extract_epi16(reg, 13) << " " << (int16_t)_mm256_extract_epi16(reg, 14) << " " << (int16_t)_mm256_extract_epi16(reg, 15) << std::endl

#define DumpI32s(reg) std::cout << #reg << " : " \
   << _mm256_extract_epi32(reg, 0) << " " << _mm256_extract_epi32(reg, 1) << " " << _mm256_extract_epi32(reg, 2) << " " << (int16_t)_mm256_extract_epi32(reg, 3) << " " \
   << _mm256_extract_epi32(reg, 4) << " " << _mm256_extract_epi32(reg, 5) << " " << _mm256_extract_epi32(reg, 6) << " " << (int16_t)_mm256_extract_epi32(reg, 7) << std::endl
#define IfDump(x) x;
#endif

// FORCEINLINE bool ConvexPolygonContainment(point2i16* openContour, int numpoints, const __m256i* segChunks, int chunkCount) {
//    auto a = (int*)_malloca(sizeof(int) * numpoints);
//    assert(a);
//
//    for (auto [i, current] = std::tuple{ 0, a }; i < numpoints; i++, current++) {
//       *current = i;
//    }
//
//    auto b = (int*)_malloca(sizeof(int) * numpoints);
// }

FORCEINLINE void LoadQuerySegmentRegisters(seg2i16 query, OUT __m256i& lhsadd, OUT __m256i& rhsleft) {
   short ax = query.x1;
   short ay = query.y1;
   short bx = query.x2;
   short by = query.y2;

   short bax = bx - ax;
   short aby = ay - by;

   // Note: This is computed before lhsadd. It improves perf 91ms->89ms reliably,
   // likely due to instruction dependency ordering.
   rhsleft = _mm256_setr_epi16(
      by, bx, // by bx
      by, bx, // by bx
      ay, ax, // ay ax
      by, bx, // by bx

      by, bx,
      by, bx,
      ay, ax,
      by, bx);
   DumpI16s(rhsleft);

   lhsadd = _mm256_setr_epi16(
      bax, aby,
      bax, aby,
      0, 0,
      0, 0,

      bax, aby,
      bax, aby,
      0, 0,
      0, 0
   );
}

FORCEINLINE void LoadQuerySegmentIntersectConstantVectors(OUT __m256i& zeros8xi32, OUT __m256i& ones8xi32, OUT __m256i& rhsrightswizzle, OUT __m256i& lhsswizzle) {
   zeros8xi32 = _mm256_setzero_si256();

   ones8xi32 = _mm256_set1_epi32(1);
   DumpI32s(ones8xi32);

   rhsrightswizzle = _mm256_setr_epi32(0, 1, 1, 1, 4, 5, 5, 5);

   lhsswizzle = _mm256_setr_epi32(3, 3, 2, 2, 7, 7, 6, 6);
}

FORCEINLINE void ComputeQuerySegmentToFourPointClocknesses(__m256i ones8xi32, __m256i rhsrightswizzle, __m256i lhsswizzle, __m256i lhsadd, __m256i rhsleft, __m256i chunk1, __m256i chunk2, OUT __m256i& clocknesses1, OUT __m256i& clocknesses2) {
   // __m256i rhsright1 = _mm256_setr_epi16(
   //    cy1, cx1,
   //    dy1, dx1,
   //    dy1, dx1,
   //    dy1, dx1,
   //
   //    cy2, cx2,
   //    dy2, dx2,
   //    dy2, dx2,
   //    dy2, dx2
   // );
   __m256i rhsright1 = _mm256_permutevar8x32_epi32(chunk1, rhsrightswizzle);
   DumpI16s(rhsright1);

   __m256i rhs1 = _mm256_sub_epi16(rhsleft, rhsright1);
   DumpI16s(rhs1);

   // __m256i rhsright2 = _mm256_setr_epi16(
   //    cy3, cx3,
   //    dy3, dx3,
   //    dy3, dx3,
   //    dy3, dx3,
   //
   //    cy4, cx4,
   //    dy4, dx4,
   //    dy4, dx4,
   //    dy4, dx4
   // );
   __m256i rhsright2 = _mm256_permutevar8x32_epi32(chunk2, rhsrightswizzle);
   DumpI16s(rhsright2);

   __m256i rhs2 = _mm256_sub_epi16(rhsleft, rhsright2);
   DumpI16s(rhs2);

   // int cdx1 = cx1 - dx1;
   // int dcy1 = dy1 - cy1;
   // int cdx2 = cx2 - dx2;
   // int dcy2 = dy2 - cy2;
   // int cdx3 = cx3 - dx3;
   // int dcy3 = dy3 - cy3;
   // int cdx4 = cx4 - dx4;
   // int dcy4 = dy4 - cy4;

   // __m256i lhs1 = _mm256_setr_epi16(
   //    bax, aby,
   //    bax, aby,
   //    cdx1, dcy1,
   //    cdx1, dcy1,
   //
   //    bax, aby,
   //    bax, aby,
   //    cdx2, dcy2,
   //    cdx2, dcy2
   // );
   __m256i lhs1 = _mm256_add_epi16(
      lhsadd,
      _mm256_permutevar8x32_epi32(chunk1, lhsswizzle)
   );
   DumpI16s(lhs1);

   // __m256i lhs2 = _mm256_setr_epi16(
   //    bax, aby,
   //    bax, aby,
   //    cdx3, dcy3,
   //    cdx3, dcy3,
   //
   //    bax, aby,
   //    bax, aby,
   //    cdx4, dcy4,
   //    cdx4, dcy4
   // );
   __m256i lhs2 = _mm256_add_epi16(
      lhsadd,
      _mm256_permutevar8x32_epi32(chunk2, lhsswizzle)
   );
   DumpI16s(lhs1);
   
   // lhs rhs    lhs rhs
   // bax .bcy + aby .bcx <---> clk(bax, bay, bcx, bcy), o1
   // bax .bdy + aby .bdx <---> clk(bax, bay, bdx, bdy), o2
   // cdx .ady + dcy .adx <---> clk(dcx, dcy, dax, day), o3
   // cdx .bdy + dcy .bdx <---> clk(dcx, dcy, dbx, dby), o4
   __m256i crosses1 = _mm256_madd_epi16(lhs1, rhs1); // Note: This gives 8 i32s
   DumpI32s(crosses1);

   clocknesses1 = _mm256_sign_epi32(ones8xi32, crosses1);
   DumpI32s(clocknesses1);

   __m256i crosses2 = _mm256_madd_epi16(lhs2, rhs2); // Note: This gives 8 i32s
   DumpI32s(crosses2);

   clocknesses2 = _mm256_sign_epi32(ones8xi32, crosses2);
   DumpI32s(clocknesses2);
}

FORCEINLINE bool AnyIntersectionsAvx2(seg2i16 query, const __m256i* segChunks, int chunkCount) {
   __m256i lhsadd, rhsleft;
   LoadQuerySegmentRegisters(query, OUT lhsadd, OUT rhsleft);

   __m256i zeros8xi32, ones8xi32, rhsrightswizzle, lhsswizzle;
   LoadQuerySegmentIntersectConstantVectors(OUT zeros8xi32, OUT ones8xi32, OUT rhsrightswizzle, OUT lhsswizzle);

   // const __m256i ones8xi32 = _mm256_set1_epi32(1);
   // DumpI32s(ones8xi32);
   //
   // const __m256i rhsrightswizzle = _mm256_setr_epi32(0, 1, 1, 1, 4, 5, 5, 5);
   //
   // const __m256i lhsswizzle = _mm256_setr_epi32(3, 3, 2, 2, 7, 7, 6, 6);
   //
   // __m256i zeros8xi32 = _mm256_setzero_si256();
   // DumpI32s(zeros8xi32);

   auto nextChunk = segChunks;
   for (auto i = 0; i < chunkCount; i += 2) {
      g_segs += 4;

      IfDump(std::cout << "iter " << i << std::endl);

      // cy1, cx1,
      // dy1, dx1,
      // cdx1, dcy1,
      // 0, 0
      __m256i chunk1 = _mm256_load_si256(nextChunk);
      DumpI16s(chunk1);

      __m256i chunk2 = _mm256_load_si256(nextChunk + 1);
      DumpI16s(chunk2);
      nextChunk += 2;

      __m256i clocknesses1, clocknesses2;
      ComputeQuerySegmentToFourPointClocknesses(ones8xi32, rhsrightswizzle, lhsswizzle, lhsadd, rhsleft, chunk1, chunk2, OUT clocknesses1, OUT clocknesses2);

      // quirk: this interweaves the horizontal subtract.
      // (g1ao1 != g1ao2, g1ao3 != g1ao4, g2ao1 != g2ao2, g2ao3 != g2ao4,
      //  g1bo1 != g1bo2, g1bo3 != g1bo4, g2bo1 != g2bo2, g2bo3 != g2bo4)
      __m256i cmp = _mm256_hsub_epi32(clocknesses1, clocknesses2); // 8x i32
      DumpI32s(cmp);

      __m256i win = _mm256_cmpeq_epi32(cmp, zeros8xi32); // (a= o1 == o2, o3 == o4, ...), 32 bits per bool.
      DumpI32s(win);

      unsigned int mask = (unsigned int)_mm256_movemask_epi8(win); // e.g. aaaabbbbccccdddd_eeeeffffgggghhhh
      IfDump(std::cout << "mask. : " << std::bitset<32>(mask) << std::endl);

      // intersect if !(o1 == o2) && !(o3 == o4) AKA bit seq 00000000 aligned to i8 boundaries
      //
      // Note mask bits above. We want something like a == 0 && b == 0
      int abits = (mask & 0b10000000100000001000000010000000); // ...... a0000000c0000000e0000000g0000000
      int bbits = (mask & 0b00001000000010000000100000001000) << 4; // . b0000000d0000000f0000000h0000000
      IfDump(std::cout << "abits : " << std::bitset<32>(abits) << std::endl);
      IfDump(std::cout << "bbits : " << std::bitset<32>(bbits) << std::endl);

      int intersects = (abits | bbits) != 0b10000000100000001000000010000000;
      IfDump(std::cout << std::endl);

      if (intersects) {
         return true;
      }
   }
   return false;
}

static thread_local short* tlsChunkBuff = nullptr;
static thread_local size_t tlsChunkBuffNumChunks = 0;

std::shared_ptr<Avx2IntersectionPrequeryState> LoadPrequeryBarriersIntersectionState(const seg2i16* barriers, int numBarriers) {
   auto numChunks = ((numBarriers + 3) / 4) * 2;
   auto chunkBuffer = _aligned_malloc(numChunks * 32, 32);
   assert(chunkBuffer);

   // zero last two chunks (4 segments) as only 1 segment might be stored & the remaining 3
   // should not detect an intersect.
   memset(static_cast<char*>(chunkBuffer) + (numChunks - 2) * 32, 0, 64);

   // Load 128 bits = half chunk = (y1, x1, y2, x2, x1 - x2, y2 - y1, 0, 0)
   auto pCurrent = reinterpret_cast<short*>(chunkBuffer);
   auto currentBarrier = barriers;
   for (auto i = 0; i < numBarriers; i++) {
      const auto barrier = *currentBarrier;
      *(pCurrent++) = barrier.y1;
      *(pCurrent++) = barrier.x1;
      *(pCurrent++) = barrier.y2;
      *(pCurrent++) = barrier.x2;
      *(pCurrent++) = barrier.x1 - barrier.x2;
      *(pCurrent++) = barrier.y2 - barrier.y1;
      *(pCurrent++) = 0;
      *(pCurrent++) = 0;
      currentBarrier++;
   }

   auto state = std::make_shared<Avx2IntersectionPrequeryState>();
   state->NumChunks = numChunks;
   state->ChunkBuffer = std::shared_ptr<char>((char*)chunkBuffer, &_aligned_free);
   return state;
}

int CountIntersectionsAvx2(std::shared_ptr<Avx2IntersectionPrequeryState> prequeryState, const std::vector<seg2i16>& queries) {
   auto numChunks = prequeryState->NumChunks;
   auto buff = (__m256i*)prequeryState->ChunkBuffer.get();

   int pass = 0;
   for (const auto& query : queries) {
      auto occluded = AnyIntersectionsAvx2(query, buff, numChunks);
      pass += !occluded;
   }

   return pass;
}

void QueryAnyIntersections(std::shared_ptr<Avx2IntersectionPrequeryState> prequeryState, const seg2i16* queries, int numQueries, uint8_t* results) {
   auto numChunks = prequeryState->NumChunks;
   auto buff = (__m256i*)prequeryState->ChunkBuffer.get();

   for (auto i = 0; i < numQueries; i++) {
      *results = AnyIntersectionsAvx2(*queries, buff, numChunks) ? 1 : 0;

      queries++;
      results++;
   }
}


int main() {
   auto barriers = parse("barriers.txt");
   auto queries = parse("queries.txt");

   auto prequeryState = LoadPrequeryBarriersIntersectionState(barriers.data(), barriers.size());
   std::cout << CountIntersections(barriers, queries) << " " << CountIntersectionsAvx2(prequeryState, queries) << std::endl;

   while (true) {
      auto niters = 10000;
      auto start = std::chrono::system_clock::now();
      g_segs = 0;
      for (auto i = 0; i < niters; i++) {
         // CountIntersections(barriers, queries);
         CountIntersectionsAvx2(prequeryState, queries);
      }
      auto end = std::chrono::system_clock::now();
      auto ms = std::chrono::duration_cast<std::chrono::duration<float, std::milli>>(end - start).count();
      std::cout << ms << " : " << ms / niters << " / " << g_segs << " : " << g_segs / ms << std::endl;
   }
}
