// ────────────────────────────────────────────────────────────────────
// ITestDataFactory.cs
// Created:  2026-02-05
// Author:   Ayu Admassu
// Purpose:  Generic factory interface for creating test data records
//           and stamping per-message headers. Implemented by Avro and
//           JSON data factories.
// ────────────────────────────────────────────────────────────────────

namespace ConfluentThroughputTestHarness.DataFactories;

public interface ITestDataFactory<T>
{
    /// <summary>Creates a template record populated with representative sample data.</summary>
    T CreateRecord();

    /// <summary>
    /// Stamps the per-message test header fields (__test_seq, __test_ts) on the record.
    /// Called before each Produce() to ensure every message has unique content,
    /// forcing the serializer to serialize fresh data each time.
    /// </summary>
    void SetMessageHeader(T record, int sequenceNumber, string timestamp);
}
