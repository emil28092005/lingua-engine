cmake_minimum_required(VERSION 3.16)
project(lingua_audio_native C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

add_library(lingua_audio SHARED lingua_audio.c)

if(UNIX AND NOT APPLE)
    find_package(Threads REQUIRED)
    target_link_libraries(lingua_audio PRIVATE Threads::Threads m dl)
endif()

install(TARGETS lingua_audio RUNTIME DESTINATION . LIBRARY DESTINATION .)
