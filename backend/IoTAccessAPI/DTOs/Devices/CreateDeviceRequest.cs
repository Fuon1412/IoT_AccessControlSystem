namespace IoTAccessAPI.DTOs.Devices;

public record CreateDeviceRequest(string Name, string MacAddress, string Location);
