#pragma once
#include "aamotion.h"
#include <string>

std::string RunOperation(aamotion_controller h, int axis, const std::string& operation, const std::string& input);
