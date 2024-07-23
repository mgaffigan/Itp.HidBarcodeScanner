// NativeHidDemo.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <string>
#include <string_view>
#include <iostream>
#include <iomanip>
#include <vector>

#include <initguid.h>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <hidsdi.h>
#pragma comment(lib, "hid.lib")

#include <setupapi.h>
#pragma comment(lib, "setupapi.lib")
#include <devpkey.h>

#include <wil/resource.h>
#include <wil/result.h>

std::vector<std::wstring> GetHidDevices(std::wstring& hardwareId)
{
	GUID hidGuid;
	HidD_GetHidGuid(&hidGuid);

	HDEVINFO hDevInfo = SetupDiGetClassDevs(&hidGuid, nullptr, nullptr, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
	THROW_LAST_ERROR_IF_MSG(hDevInfo == INVALID_HANDLE_VALUE, "SetupDiGetClassDevs failed");

	std::vector<std::wstring> devices;

	SP_DEVICE_INTERFACE_DATA deviceInterfaceData;
	deviceInterfaceData.cbSize = sizeof(SP_DEVICE_INTERFACE_DATA);
	for (DWORD i = 0; ; i++)
	{
		if (!SetupDiEnumDeviceInterfaces(hDevInfo, nullptr, &hidGuid, i, &deviceInterfaceData))
		{
			if (GetLastError() == ERROR_NO_MORE_ITEMS) break;
			THROW_LAST_ERROR_MSG("SetupDiEnumDeviceInterfaces failed");
		}

		// Get SP_DEVINGFO_DATA and path
		DWORD requiredSize = 0;
		if (SetupDiGetDeviceInterfaceDetailW(hDevInfo, &deviceInterfaceData, nullptr, 0, &requiredSize, nullptr))
		{
			throw std::runtime_error("SetupDiGetDeviceInterfaceDetail unexpectedly succeeded");
		}
		THROW_LAST_ERROR_IF(GetLastError() != ERROR_INSUFFICIENT_BUFFER);

		std::unique_ptr<char[]> buffer(new char[requiredSize]);
		SP_DEVICE_INTERFACE_DETAIL_DATA* deviceInterfaceDetailData = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA*>(buffer.get());
		deviceInterfaceDetailData->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);
		SP_DEVINFO_DATA deviceInfoData = { sizeof(SP_DEVINFO_DATA) };
		THROW_LAST_ERROR_IF(!SetupDiGetDeviceInterfaceDetailW(hDevInfo, &deviceInterfaceData, deviceInterfaceDetailData, requiredSize, &requiredSize, &deviceInfoData));

		// Check if hardwareId matches DEVPKEY_Device_HardwareIds
		requiredSize = 0;
		DEVPROPTYPE property_type = 0;
		if (SetupDiGetDevicePropertyW(hDevInfo, &deviceInfoData, &DEVPKEY_Device_HardwareIds, &property_type, nullptr, 0, &requiredSize, 0))
		{
			throw std::runtime_error("SetupDiGetDeviceProperty unexpectedly succeeded");
		}
		THROW_LAST_ERROR_IF(GetLastError() != ERROR_INSUFFICIENT_BUFFER);
		if (property_type != DEVPROP_TYPE_STRING_LIST)
		{
			throw std::runtime_error("Unexpected property type");
		}

		std::wstring hardwareIds;
		hardwareIds.resize(requiredSize / sizeof(wchar_t));
		THROW_LAST_ERROR_IF(!SetupDiGetDevicePropertyW(hDevInfo, &deviceInfoData, &DEVPKEY_Device_HardwareIds, &property_type, 
			reinterpret_cast<PBYTE>(&hardwareIds[0]), requiredSize, nullptr, 0));

		// hardwareIds is a list of null-terminated strings, loop and check if any match
		size_t start = 0;
		size_t end = hardwareIds.find(L'\0');
		while (end != std::wstring::npos)
		{
			if (hardwareId == hardwareIds.substr(start, end - start))
			{
				devices.emplace_back(deviceInterfaceDetailData->DevicePath);
				break;
			}
			start = end + 1;
			end = hardwareIds.find(L'\0', start);
		}
	}

	SetupDiDestroyDeviceInfoList(hDevInfo);

	return devices;
}

// add wil unique_ptr for HidD_FreePreparsedData
typedef wil::unique_any<PHIDP_PREPARSED_DATA, decltype(&::HidD_FreePreparsedData), ::HidD_FreePreparsedData> unique_hid_preparsed_data;

void listen_to_scanner(const std::wstring& path)
{
	wil::unique_handle hDevice(CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr));
	THROW_LAST_ERROR_IF_MSG(!hDevice.is_valid(), "CreateFile failed");

	// Get preparsed data
	unique_hid_preparsed_data preparsedData;
	THROW_LAST_ERROR_IF(!HidD_GetPreparsedData(hDevice.get(), &preparsedData));

	// Get HIDP_CAPS
	HIDP_CAPS caps;
	THROW_IF_NTSTATUS_FAILED(HidP_GetCaps(preparsedData.get(), &caps));

	std::unique_ptr<HIDP_VALUE_CAPS[]> inputCaps(new HIDP_VALUE_CAPS[caps.NumberInputValueCaps]);
	THROW_IF_NTSTATUS_FAILED(HidP_GetSpecificValueCaps(HidP_Input, 0, 0, 0, inputCaps.get(), &caps.NumberInputValueCaps, preparsedData.get()));

	std::unique_ptr<HIDP_BUTTON_CAPS[]> buttonCaps(new HIDP_BUTTON_CAPS[caps.NumberInputButtonCaps]);
	THROW_IF_NTSTATUS_FAILED(HidP_GetButtonCaps(HidP_Input, buttonCaps.get(), &caps.NumberInputButtonCaps, preparsedData.get()));

	HIDP_VALUE_CAPS textCap;
	USHORT numCaps = 1;
	THROW_IF_NTSTATUS_FAILED(HidP_GetSpecificValueCaps(HidP_Input, 0x8c, 0, 0xfe, &textCap, &numCaps, preparsedData.get()));

	// Allocate buffer for input report
	BYTE buf1[64] = { 0 };
	CHAR buf2[64] = { 0 };

	// Prepare OVERLAPPED structure
	OVERLAPPED overlapped = { 0 };
	overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
	THROW_LAST_ERROR_IF_MSG(overlapped.hEvent == nullptr, "CreateEvent failed");

	// Start reading
	while (true)
	{
		DWORD bytesRead = 0;
		if (ReadFile(hDevice.get(), buf1, caps.InputReportByteLength, nullptr, &overlapped))
		{
			throw std::runtime_error("ReadFile unexpectedly succeeded");
		}
		THROW_LAST_ERROR_IF_MSG(GetLastError() != ERROR_IO_PENDING, "ReadFile failed");

		// Wait for read to complete
		WaitForSingleObject(overlapped.hEvent, INFINITE);

		// Get result
		THROW_LAST_ERROR_IF(!GetOverlappedResult(hDevice.get(), &overlapped, &bytesRead, TRUE));
		std::wcout << L"Read " << bytesRead << L" bytes" << std::endl;

		// Convert to hex
		std::wcout << L"Raw  " ;
		for (DWORD i = 0; i < bytesRead; i++)
		{
			std::wcout << std::hex << std::setfill(L'0') << std::setw(2) << (int)buf1[i];
		}
		std::wcout << std::endl;

		// Parse input report
		memset(buf2, 0, sizeof(buf2));
		auto status = HidP_GetUsageValueArray(HidP_Input, 0x8c, 0, 0xfe, buf2, sizeof(buf2), preparsedData.get(), (PCHAR)buf1, bytesRead);
		if (status != HIDP_STATUS_SUCCESS)
		{
			std::wcout << L"HidP_GetUsageValueArray failed with status " << status << std::endl;
			continue;
		}
		std::cout << "Data " << buf2 << std::endl;

		// Print each value
		for (int i = 0; i < caps.NumberInputValueCaps; i++)
		{
			if (inputCaps[i].Range.UsageMin == 0xfe) continue;

			ULONG value = 0;
			auto result = HidP_GetUsageValue(HidP_Input, inputCaps[i].UsagePage, 0, inputCaps[i].Range.UsageMin, &value, preparsedData.get(), (PCHAR)buf1, bytesRead);
			if (result != HIDP_STATUS_SUCCESS)
			{
				std::wcout << L"HidP_GetUsageValue " << std::hex << inputCaps[i].Range.UsageMin << L" failed with status " << result << std::endl;
				continue;
			}

			std::wcout << L"Usage " << std::hex << inputCaps[i].Range.UsageMin << L" page " << inputCaps[i].UsagePage << L" = " << value << std::endl;
		}

		// Print each button
		USAGE usages[256];
		ULONG usageCount = 256;
		THROW_IF_NTSTATUS_FAILED(HidP_GetUsages(HidP_Input, 0, 0, usages, &usageCount, preparsedData.get(), (PCHAR)buf1, bytesRead));
		for (ULONG i = 0; i < usageCount; i++)
		{
			std::wcout << L"Button " << std::hex << usages[i] << std::endl;
		}
	}
}

int main()
{
	auto target = std::wstring(L"HID_DEVICE_UP:008C_U:0002");
	std::vector<std::wstring> devices = GetHidDevices(target);

	for (const std::wstring& device : devices)
	{
		std::wcout << device << std::endl;
		listen_to_scanner(device);
	}
    return 0;
}
