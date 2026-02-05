// ────────────────────────────────────────────────────────────────────
// ITestDataFactory.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Generic factory interface for creating test data records.
//           Implemented by Avro and JSON data factories to produce
//           a single representative record for throughput benchmarks.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.DataFactories;

public interface ITestDataFactory<T>
{
    T CreateRecord();
}
