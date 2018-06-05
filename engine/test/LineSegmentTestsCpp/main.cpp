#include <cstdint>
#include <iostream>
#include <fstream>
#include <vector>
#include <chrono>
#include <stack>
#include "clipper.hpp"
#include <iomanip>

typedef int32_t cInt;

enum Clckness : int {
   Clockwise = -1,
   Neither = 0,
   CounterClockwise = 1
};

struct IntVector2
{
   cInt x;
   cInt y;
};

struct IntLineSegment2
{
   IntVector2 first;
   IntVector2 second;
};

template <typename T> 
int sign(T val) {
   return (T(0) < val) - (val < T(0));
}


static int64_t Cross(cInt ax, cInt ay, cInt bx, cInt by) {
   return (int64_t)ax * by - (int64_t)ay * bx;
}


Clckness Clockness(cInt bax, cInt bay, cInt bcx, cInt bcy) {
   return static_cast<Clckness>(sign(Cross(bax, bay, bcx, bcy)));
}

Clckness Clockness(cInt ax, cInt ay, cInt bx, cInt by, cInt cx, cInt cy) {
   return Clockness(bx - ax, by - ay, bx - cx, by - cy);
}


bool Intersects(cInt ax, cInt ay, cInt bx, cInt by, cInt cx, cInt cy, cInt dx, cInt dy) {
   auto o1 = Clockness(ax, ay, bx, by, cx, cy);
   auto o2 = Clockness(ax, ay, bx, by, dx, dy);
   auto o3 = Clockness(cx, cy, dx, dy, ax, ay);
   auto o4 = Clockness(cx, cy, dx, dy, bx, by);

   if (o1 != o2 && o3 != o4) return true;

//   if (o1 == 0 && new IntLineSegment2(ax, ay, bx, by).Contains(new IntVector2(cx, cy))) return true;
//   if (o2 == 0 && new IntLineSegment2(ax, ay, bx, by).Contains(new IntVector2(dx, dy))) return true;
//   if (o3 == 0 && new IntLineSegment2(cx, cy, dx, dy).Contains(new IntVector2(ax, ay))) return true;
//   if (o4 == 0 && new IntLineSegment2(cx, cy, dx, dy).Contains(new IntVector2(bx, by))) return true;

   return false;
}


bool intersects(IntLineSegment2& a, IntLineSegment2& b) {
   cInt ax = a.first.x, ay = a.first.y, bx = a.second.x, by = a.second.y;
   cInt cx = b.first.x, cy = b.first.y, dx = b.second.x, dy = b.second.y;
   return Intersects(ax, ay, bx, by, cx, cy, dx, dy);
}

void runTrial(IntLineSegment2* ls, int n) {
   long count = 0;
   for (auto i = 0; i < n; i++) {
      for (auto j = 0; j < n; j++) {
         if (intersects(ls[i], ls[j])) {
            count++;
         }
      }
   }
   std::cout << count << std::endl;
}

void runTrial2(ClipperLib::Paths& included, ClipperLib::Paths& excluded) {
   ClipperLib::Clipper x { ClipperLib::ioStrictlySimple };
   x.AddPaths(included, ClipperLib::ptSubject, true);
   x.AddPaths(excluded, ClipperLib::ptClip, true);

   // std::cout << included.size() << " " << excluded.size() << std::endl;

   ClipperLib::PolyTree res{};
   x.Execute(ClipperLib::ctDifference, res, ClipperLib::pftPositive);
   return;

   std::function<void(ClipperLib::PolyNode*, int)> R = [&](ClipperLib::PolyNode* n, int depth) {
      std::cout << ":";
      for (auto i = 0; i < depth; i++) {
         std::cout << " ";
      }
      
      int counter = 0;
      for (auto& p : n->Contour) {
         std::cout << p.X << " " << p.Y << (counter + 1 == n->Contour.size() ? "" : ", ");
         counter++;
      }
      std::cout << std::endl;

      //for (auto x : n->Childs) {
      for (auto it = n->Childs.rbegin(); 
           it != n->Childs.rend();
           ++it) {
         R(*it, depth + 1);
      }
   };

   R(&res, 0);
   while (true) srand(0);
}

int main() {
   std::cout << std::setprecision(10) << std::fixed;

   //auto y = R"A(v:\my-repositories\miyu\derp\TestProjects\LineSegmentTestsCpp\segments.txt)A";
   auto y = R"A(v:\my-repositories\miyu\derp\TestProjects\LineSegmentTestsCpp\test2d.txt)A";
   std::fstream fs(y, std::fstream::in);

   ClipperLib::Paths included, excluded;

   int a;
   while (fs >> a) {
      ClipperLib::Path path;
      
      int c;  
      fs >> c;

      for (auto i = 0; i < c; i++) {
         int x, y;
         fs >> x;
         fs >> y;

         path.push_back({ x, y });
      }

      if (a == 0) {
         included.push_back(path);
      } else {
         excluded.push_back(path);
      }
   }

//   int a, b, c, d;
//   std::vector<IntLineSegment2> ls; 
//   while (fs >> a && fs >> b && fs >> c && fs >> d) {
//      //std::cout << a << " " << b << " " << c << " " << d << std::endl;
//      ls.push_back({ {a, b}, {c ,d} });
//   }

   for (auto trial = 0; trial < 50000; trial++) {
      runTrial2(included, excluded);
   }

   std::cout << "Starting" << std::endl;

   std::chrono::high_resolution_clock::time_point t1 = std::chrono::high_resolution_clock::now();

   for (auto trial = 0; trial < 100000; trial++) {
      runTrial2(included, excluded);
   }

   std::chrono::high_resolution_clock::time_point t2 = std::chrono::high_resolution_clock::now();
   std::chrono::duration<double> time_span = std::chrono::duration_cast<std::chrono::duration<double>>(t2 - t1);

   std::cout << "It took me " << time_span.count() << " seconds.";
   std::cout << std::endl;

   while (true) {
      srand(0);
   }
   return 0;
}