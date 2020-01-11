// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

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

struct seg2i16 {
   short x1, y1, x2, y2;
};

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

bool anyIntersections(seg2i16 query, const std::vector<seg2i16>& segments, bool detectEndpointContainment) {
   short ax = query.x1;
   short ay = query.y1;
   short bx = query.x2;
   short by = query.y2;

   short bax = bx - ax;
   short bay = by - ay;

   for (const auto& seg : segments) {
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
      if (o1 != o2 && o3 != o4) return true;

      if (detectEndpointContainment) {
         throw std::exception("not implemented");
      }
   }
   return false;
}

int benchIter(const std::vector<seg2i16>& barriers, const std::vector<seg2i16>& queries) {
   int pass = 0;
   for (const auto& query : queries) {
      auto occluded = anyIntersections(query, barriers, false);
      pass += !occluded;
   }

   return pass;
}

bool anyIntersectionsAvx2(seg2i16 query, const std::vector<seg2i16>& segments, bool detectEndpointContainment) {
   short ax = query.x1;
   short ay = query.y1;
   short bx = query.x2;
   short by = query.y2;

   short bax = bx - ax;
   short bay = by - ay;

   auto pCurrent = (const short*)(&segments[0]);
   auto simdIters = segments.size() / 2;
   for (auto i = 0; i < simdIters; i++) {
      #define val(i, n) (pCurrent[i * 4 + n])

      __m256i t = _mm256_set_epi16(
         query.y2, query.x2,
         query.y2, query.x2,
         query.y1, query.x1,
         query.y2, query.x2,

         query.y2, query.x2,
         query.y2, query.x2,
         query.y1, query.x1,
         query.y2, query.x2);

      short cx1 = *(pCurrent++);
      short cy1 = *(pCurrent++);
      short dx1 = *(pCurrent++);
      short dy1 = *(pCurrent++);
      short cx2 = *(pCurrent++);
      short cy2 = *(pCurrent++);
      short dx2 = *(pCurrent++);
      short dy2 = *(pCurrent++);

      __m256i p = _mm256_set_epi16(
         cy1, cx1,
         dy1, dx1,
         dy1, dx1,
         dy1, dx1,

         cy2, cx2,
         dy2, dx2,
         dy2, dx2,
         dy2, dx2
      );

      __m256i q = _mm256_mullo_epi16(t, p);

      short bcx1 = bx - cx1;
      short bcy1 = by - cy1;
      short bdx1 = bx - dx1;
      short bdy1 = by - dy1;

      short bcx2 = bx - cx2;
      short bcy2 = by - cy2;
      short bdx2 = bx - dx2;
      short bdy2 = by - dy2;

      __m256i lhs = _mm256_set_epi16(
         bax, -bay,
         bax, -bay,
         cx1 - dx1, dy1 - cy1,
         cx1 - dx1, dy1 - cy1,

         bax, -bay,
         bax, -bay,
         cx2 - dx2, dy2 - cy2,
         cx2 - dx2, dy2 - cy2
      );

      __m256i crosses = _mm256_madd_epi16(lhs, q);


      // bax .bcy - bay .bcx
      // bax .bdy - bay .bdx
      // dcx .day - dcy .dax
      // dcx .dby - dcy .dbx

      // bax .bcy + aby .bcx
      // bax .bdy + aby .bdx
      // -dcx .ady - cdy .adx
      // -dcx .bdy - cdy .bdx


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
      if (o1 != o2 && o3 != o4) return true;

      if (detectEndpointContainment) {
         throw std::exception("not implemented");
      }
   }
   return false;
}

int benchIterAvx2(const std::vector<seg2i16>& barriers, const std::vector<seg2i16>& queries) {
   int pass = 0;
   for (const auto& query : queries) {
      auto occluded = anyIntersectionsAvx2(query, barriers, false);
      pass += !occluded;
   }

   return pass;
}

int main() {
   auto barriers = parse("barriers.txt");
   auto queries = parse("queries.txt");

   auto pass = benchIter(barriers, queries);
   std::cout << pass << std::endl;

   while (true) {
      auto niters = 10000;
      auto start = std::chrono::system_clock::now();
      for (auto i = 0; i < niters; i++) {
         benchIterAvx2(barriers, queries);
      }
      auto end = std::chrono::system_clock::now();
      auto ms = std::chrono::duration_cast<std::chrono::duration<float, std::milli>>(end - start);
      std::cout << ms.count() << " : " << ms.count() / niters << std::endl;
   }

   __m256i a = _mm256_set_epi32(1, 2, 3, 4, 5, 6, 7, 8);
   __m256i b = _mm256_set_epi32(10, 20, 30, 40, 50, 60, 70, 80);
}
