cmake_minimum_required(VERSION 3.20)
project(AxisPanel CXX RC)

set(CMAKE_CXX_STANDARD 17)
set(AAMOTION_SDK_DIR "" CACHE PATH "Folder of the extracted agito-aamotion-native-sdk zip")
if(NOT AAMOTION_SDK_DIR)
  message(FATAL_ERROR "Set AAMOTION_SDK_DIR to the extracted native SDK folder")
endif()
if(NOT WIN32)
  message(FATAL_ERROR "AxisPanel is a Win32 program: build it on Windows")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "aamotion is 64-bit only: configure an x64 build")
endif()

add_executable(AxisPanel WIN32
  main.cpp MainWindow.cpp AxisController.cpp AxisOperations.cpp AxisPanel.rc)
target_include_directories(AxisPanel PRIVATE "${AAMOTION_SDK_DIR}/include")
target_compile_definitions(AxisPanel PRIVATE UNICODE _UNICODE)
target_link_libraries(AxisPanel PRIVATE "${AAMOTION_SDK_DIR}/win-x64/aamotion.lib" comctl32)
# The sources are UTF-8 with Chinese text; MSVC otherwise reads them in the system code page.
if(MSVC)
  target_compile_options(AxisPanel PRIVATE /utf-8)
endif()
# The library and the AACommServerNative daemon must sit beside the program.
foreach(file aamotion.dll AACommServerNative.exe)
  add_custom_command(TARGET AxisPanel POST_BUILD COMMAND ${CMAKE_COMMAND} -E copy_if_different
    "${AAMOTION_SDK_DIR}/win-x64/${file}" $<TARGET_FILE_DIR:AxisPanel>)
endforeach()
add_custom_command(TARGET AxisPanel POST_BUILD COMMAND ${CMAKE_COMMAND} -E copy_if_different
  "${CMAKE_CURRENT_SOURCE_DIR}/../../operations.tsv" $<TARGET_FILE_DIR:AxisPanel>)
