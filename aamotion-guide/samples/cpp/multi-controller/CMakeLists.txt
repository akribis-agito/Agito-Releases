cmake_minimum_required(VERSION 3.20)
project(multi_controller CXX)

set(CMAKE_CXX_STANDARD 17)
set(AAMOTION_SDK_DIR "" CACHE PATH "Folder of the extracted agito-aamotion-native-sdk zip")
if(NOT AAMOTION_SDK_DIR)
  message(FATAL_ERROR "Set AAMOTION_SDK_DIR to the extracted native SDK folder")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "aamotion is 64-bit only: configure an x64 build")
endif()

add_executable(multi_controller main.cpp)
target_include_directories(multi_controller PRIVATE "${AAMOTION_SDK_DIR}/include")
if(WIN32)
  target_link_libraries(multi_controller PRIVATE "${AAMOTION_SDK_DIR}/win-x64/aamotion.lib")
  add_custom_command(TARGET multi_controller POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${AAMOTION_SDK_DIR}/win-x64" $<TARGET_FILE_DIR:multi_controller>)
else()
  target_link_libraries(multi_controller PRIVATE "${AAMOTION_SDK_DIR}/linux-x64/libaamotion.so")
  set_target_properties(multi_controller PROPERTIES BUILD_RPATH "$ORIGIN")
  add_custom_command(TARGET multi_controller POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${AAMOTION_SDK_DIR}/linux-x64" $<TARGET_FILE_DIR:multi_controller>)
endif()
