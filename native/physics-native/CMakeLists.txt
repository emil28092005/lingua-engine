cmake_minimum_required(VERSION 3.16)
project(lingua_physics_native C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Box3D built static and folded into our own shared library, so the engine
# ships and P/Invokes exactly one native file (lingua_physics.so/.dll), not
# two. Pinned to a specific commit, not a tag or branch: Box3D has no 1.0
# release yet and its API is still moving (confirmed by diffing its only
# tag, v0.1.0, against this commit — real, not hypothetical, drift), so a
# floating reference would silently start building against a different API
# than the one this shim's code was actually written and verified against.
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(BOX3D_SAMPLES OFF CACHE BOOL "" FORCE)
set(BOX3D_UNIT_TESTS OFF CACHE BOOL "" FORCE)
set(BOX3D_BENCHMARKS OFF CACHE BOOL "" FORCE)
set(BOX3D_DOCS OFF CACHE BOOL "" FORCE)

include(FetchContent)
FetchContent_Declare(
    box3d
    GIT_REPOSITORY https://github.com/erincatto/box3d.git
    GIT_TAG 47d7f7cc7e091142c08d11dc7d2e493c5d34f536
)
FetchContent_MakeAvailable(box3d)

add_library(lingua_physics SHARED lingua_physics.c)
target_link_libraries(lingua_physics PRIVATE box3d)

install(TARGETS lingua_physics RUNTIME DESTINATION . LIBRARY DESTINATION .)
