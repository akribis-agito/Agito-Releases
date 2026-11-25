cmake_minimum_required(VERSION 3.20)
project(sample01_connect CXX)

set(CMAKE_CXX_STANDARD 17)
set(AAMOTION_SDK_DIR "" CACHE PATH "Folder of the extracted agito-aamotion-native-sdk zip")
if(NOT AAMOTION_SDK_DIR)
  message(FATAL_ERROR "Set AAMOTION_SDK_DIR to the extracted native SDK folder")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "aamotion is 64-bit only: configure an x64 build")
endif()

add_executable(sample01_connect main.cpp)
target_include_directories(sample01_connect PRIVATE "${AAMOTION_SDK_DIR}/include")
if(WIN32)
  target_link_libraries(sample01_connect PRIVATE "${AAMOTION_SDK_DIR}/win-x64/aamotion.lib")
  add_custom_command(TARGET sample01_connect POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${AAMOTION_SDK_DIR}/win-x64" $<TARGET_FILE_DIR:sample01_connect>)
else()
  target_link_libraries(sample01_connect PRIVATE "${AAMOTION_SDK_DIR}/linux-x64/libaamotion.so")
  set_target_properties(sample01_connect PROPERTIES BUILD_RPATH "$ORIGIN")
  add_custom_command(TARGET sample01_connect POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${AAMOTION_SDK_DIR}/linux-x64" $<TARGET_FILE_DIR:sample01_connect>)
endif()
