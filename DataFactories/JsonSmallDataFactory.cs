using ConfluentThroughputTestHarness.Models;

namespace ConfluentThroughputTestHarness.DataFactories;

public class JsonSmallDataFactory : ITestDataFactory<TestAvroDataTypesMsg>
{
    public TestAvroDataTypesMsg CreateRecord()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nowMicros = now * 1000;
        var guid = Guid.NewGuid().ToString();

        return new TestAvroDataTypesMsg
        {
            Guid1 = guid,
            Guid1N = guid,
            Guid1NV = guid,
            Bool1 = true,
            Bool1N = true,
            Bool1NV = false,
            String1 = "TestStringValue",
            String1N = "NullableStringWithValue",
            String1NV = "AnotherString",
            Int1 = 42,
            Int1N = 100,
            Int1NV = 200,
            Long1 = 123456789L,
            Long1N = 987654321L,
            Long1NV = 555555555L,
            Float1 = 3.14f,
            Float1N = 2.718f,
            Float1NV = 1.414f,
            Double1 = 3.14159265358979,
            Double1N = 2.71828182845905,
            Double1NV = 1.41421356237310,
            Decimal1 = 1234567.8m,
            Decimal1N = 9876543.2m,
            Decimal1NV = 1111111.1m,
            Decimal2 = 123456.78m,
            Decimal2N = 987654.32m,
            Decimal2NV = 111111.11m,
            Decimal4 = 1234.5678m,
            Decimal4N = 9876.5432m,
            Decimal4NV = 1111.1111m,
            DTUnspecified = now,
            DTUnspecifiedN = now,
            DTUnspecifiedNV = now,
            DTUTC = now,
            DTUTCN = now,
            DTUTCNV = now,
            DTUTCMillis = now,
            DTUTCMicros = nowMicros,
            DTLocal = now,
            DTLocalN = now,
            DTLocalNV = now,
            DTLocalMillis = now,
            DTLocalMicros = nowMicros,
            DTOUTC1 = now,
            DTOUTC1N = now,
            DTOUTC1NV = now,
            DTOLocal1 = now,
            DTOLocal1N = now,
            DTOLocal1NV = now,
            OrderStatus1 = "Shipped",
            OrderStatus1N = "Pending",
            OrderStatus1NV = "Delivered"
        };
    }
}
