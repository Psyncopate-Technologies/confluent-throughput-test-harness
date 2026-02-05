namespace ConfluentThroughputTestHarness.DataFactories;

public interface ITestDataFactory<T>
{
    T CreateRecord();
}
