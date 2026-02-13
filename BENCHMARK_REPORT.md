# Confluent Kafka .NET Producer & Consumer Throughput Benchmark Report

**Test Date:** February 13, 2026
**Prepared for:** TQL
**Cluster:** Confluent Cloud (us-east-2, AWS)

---

## 1. Executive Summary

We benchmarked four producer patterns and two consumer commit strategies against a Confluent Cloud Kafka cluster using the Confluent.Kafka .NET client (v2.13.0). All tests used production-grade settings (`acks=all`, `enable.idempotence=true`, LZ4 compression) and ran in hybrid mode (100,000 messages or 1 minute, whichever comes first), with 3-run averages reported.

**Top-line findings:**

- **Fire-and-forget** delivers the highest producer throughput by orders of magnitude: **28,963 msgs/sec** for Avro Small -- 4,163x faster than request-response.
- **Request-response** (await each message) caps at **~7 msgs/sec** regardless of format or payload size -- the bottleneck is pure network round-trip latency (~143 ms).
- **Batch concurrency windows** scale linearly in a tight loop: Window-1 matches request-response, Window-10 delivers ~70 msgs/sec, Window-100 reaches ~668 msgs/sec.
- **Under realistic conditions** (100ms inter-message delay), Window-10 and Window-100 collapse to the **same ~9 msgs/sec** because the batch deadline (500ms) fires before the window fills, making window size irrelevant.
- **Consumer batch commit** is **86x faster** than per-message commit -- the single largest performance lever in the entire benchmark.
- **Avro vs JSON: format only matters when the producer is CPU-bound.** In fire-and-forget (the only CPU/serialization-bound pattern), Avro Large outperforms JSON Large by 3x (17,884 vs 6,030 msgs/sec). In request-response and batch concurrency window patterns, network round-trip latency (~143ms) completely dominates, making Avro and JSON throughput virtually identical.

---

## 2. Test Environment & Configuration

### 2.1 Infrastructure

| Setting | Value |
|---------|-------|
| **Platform** | Confluent Cloud (AWS us-east-2) |
| **Security** | SaslSsl |
| **Topics** | 1 partition each, 1-hour retention |
| **Client** | Confluent.Kafka .NET v2.13.0 |

### 2.2 Producer Configuration Properties

All producers used `acks=all` + `enable.idempotence=true` for production-grade durability guarantees.

| Property | .NET Library Default (if unset) | Test Value | What It Does | Why It Matters |
|----------|---------------------------------|------------|--------------|----------------|
| `acks` | `all` (-1) | `all` | The producer waits for acknowledgment from the leader and all in-sync replicas (ISR) before considering a message successful. | Provides strong durability. Prevents data loss if the leader broker fails immediately after write. Slightly increases latency. |
| `enable.idempotence` | `false` | **`true`** | Ensures messages are written exactly once per partition, preventing duplicates caused by retries. | Guarantees no duplicate messages during retries. Automatically sets: `acks=all`, `retries=MAX_INT`, and `max.in.flight.requests.per.connection` <= 5. Essential for safe production workloads. With idempotence enabled and `max.in.flight.requests.per.connection` <= 5, messages are guaranteed to be delivered in order per partition, even during retries. Without idempotence, out-of-order delivery is possible when retries occur. |
| `linger.ms` | `5` | **`100`** | Producer waits up to this many milliseconds before sending a batch, allowing more records to accumulate. | Improves batching efficiency and throughput. Increases latency slightly but reduces network calls and improves compression ratio. The test value of 100ms is significantly higher than the 5ms default, allowing much larger batches to form. |
| `batch.size` | `1,000,000` (~1 MB) | `1,000,000` (~1 MB) | Maximum size of a batch (in bytes) per partition before sending to broker. | Larger batches improve throughput and compression efficiency, but increase memory usage and potential latency under low traffic. |
| `compression.type` | `none` | **`lz4`** | Compresses message batches using the specified algorithm before sending. | LZ4 reduces network bandwidth and storage usage with very low CPU overhead. Good balance of speed and compression ratio for high-throughput systems. |

### 2.3 Consumer Configuration Properties

All consumers used `enable.auto.commit=false` with manual offset commit for explicit control over commit timing.

| Property | .NET Library Default (if unset) | Test Value | What It Does | Why It Matters |
|----------|---------------------------------|------------|--------------|----------------|
| `group.id` | *(required, no default)* | `throughput-test-{TestId}-run-{N}-{GUID}` | Identifies the consumer group this consumer belongs to. Kafka uses the group ID to track committed offsets and coordinate partition assignment across group members. | A unique GUID-suffixed group ID per run ensures each benchmark reads the full topic from offset 0, with no interference from prior runs. In production, a stable group ID enables offset tracking and consumer rebalancing. |
| `auto.offset.reset` | `largest` (latest) | **`earliest`** | Determines where to start consuming when no committed offset exists for the consumer group: `earliest` = beginning of topic, `latest` = end of topic (new messages only). | Set to `earliest` so each new consumer group (unique per run) reads the full topic from the beginning. In production, `earliest` is safer for new deployments (no missed messages); `latest` is used when only new messages matter. |
| `enable.auto.commit` | `true` | **`false`** | When `true`, the client automatically commits offsets in the background at a fixed interval (`auto.commit.interval.ms`, default 5s). When `false`, the application must call `consumer.Commit()` explicitly. | Disabled to give the benchmark full control over when offsets are committed. This is the recommended pattern for production workloads where "at-least-once" or "exactly-once" processing guarantees are required -- you commit only after successful processing, not on a timer. |
| `fetch.min.bytes` | `1` | `1` | Minimum amount of data (bytes) the broker should return for a fetch request. If not enough data is available, the broker waits up to `fetch.wait.max.ms` before responding. | Set to 1 (the default) so the consumer receives messages as soon as they are available with no artificial delay. Higher values trade latency for efficiency by allowing the broker to accumulate more data per fetch. |
| `fetch.max.bytes` | `52,428,800` (50 MB) | `52,428,800` (50 MB) | Maximum total data the broker returns across all partitions in a single fetch response. | Set to 50 MB (the default) to allow large fetch batches during throughput testing. This is a global limit per fetch request; individual partitions are further constrained by `max.partition.fetch.bytes`. |
| `max.partition.fetch.bytes` | `1,048,576` (1 MB) | **`10,485,760` (10 MB)** | Maximum data the broker returns for a single partition per fetch request. | Increased to 10 MB (10x the default) to allow the consumer to pull large batches from each partition in a single fetch, maximizing throughput. In production, increase this if messages are large or if you want fewer fetch round-trips. |
| `enable.partition.eof` | `false` | **`true`** | When `true`, the consumer receives a special EOF event when it reaches the end of a partition's log. | Enabled so the consume loop can detect when the topic is fully drained. The benchmark skips these events (they are informational, not real messages). |

### 2.4 Test Execution Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| Message Count Limit | 100,000 | Max messages per test run |
| Time Limit | 1 minute | Max duration per test run |
| Termination Mode | Hybrid | Stops at whichever limit is reached first |
| Producer Runs | 3 | Each producer test is executed 3 times; averages reported |
| Consumer Runs | 3 | Each consumer test is executed 3 times; averages reported |
| BatchTimeoutMs | 500 ms | Time-bound deadline for batch event collection in T3.x/T3B.x concurrency window loop |
| InterMessageDelayMs | 100 ms | Delay between `ProduceAsync` calls in T3B.x business-realistic tests only (T3.x = 0) |
| CommitBatchSize | 100 | Messages between offset commits in ManualBatch consumer tests |
| CommitIntervalMs | 500 ms | Time-based commit interval for ManualBatch consumer tests |

---

## 3. Producer Pattern Guide

| Scenario | Recommended Pattern | Rationale | Pros | Cons |
|----------|-------------------|-----------|------|------|
| **Maximum throughput, async errors OK** -- You care most about sending as many messages per second as possible, and you're fine handling delivery failures asynchronously. | `Produce` + delivery handler (Fire-and-forget) | Fully non-blocking; allows librdkafka to batch efficiently | Highest possible throughput. Zero Task overhead. Maximum batching efficiency. Minimal latency per call. | Errors handled later in callback. Harder retry logic. Less explicit backpressure control. Can overwhelm app memory if not careful. |
| **Simple request-response service** (e.g., Microservice) | `ProduceAsync` + `await` | Straightforward flow control with natural backpressure | Easy to reason about. Exceptions propagate naturally. Built-in backpressure. Clean, linear code. | Lower throughput. Adds latency per request. Less batching efficiency. |
| **Background batch processing** | `ProduceAsync` + `Task.WhenAll(N)` (Controlled async) + time-bound deadline | Controlled concurrency with inline error handling | Tunable concurrency (N). Structured error handling. Good balance of speed and control. Predictable memory usage. | Slight task allocation overhead. Slightly less efficient batching than fire-and-forget. Requires manual tuning of N. |
| **Transactional (exactly-once)** | `ProduceAsync` within transactions | Required for transactional producer semantics | Exactly-once guarantees. Atomic multi-message commits. Strong correctness model. | Highest latency. Lower throughput. More complex error handling. Requires idempotence + proper config. |

## 4. Consumer Pattern Guide

| Scenario | Recommended Pattern | Rationale | Pros | Cons |
|----------|-------------------|-----------|------|------|
| **Strictest delivery guarantee** -- Every message must be confirmed processed before the offset is committed. No message can be skipped or lost on failure. | `consumer.Commit(result)` after every message (ManualPerMessage) | Guarantees no message is marked committed until the application has fully processed it. | Strongest at-least-once guarantee. Simplest failure recovery (reprocessing starts from last committed offset, at most 1 message re-delivered). Easiest to reason about. | Very low throughput (~24 msgs/sec) -- each commit is a synchronous broker round-trip (~41ms). CPU mostly idle, waiting on network. |
| **High-throughput consumption** -- You need to maximize read performance and can tolerate reprocessing a small batch of messages on failure. | `consumer.Commit()` every N messages or on a time interval (ManualBatch) | Amortizes commit overhead across many messages, dramatically reducing broker round-trips. | **86x faster** than per-message commit. Tunable batch size and time interval. Most of the time is spent consuming, not waiting on commits. | On failure, up to N messages (or one interval's worth) may be reprocessed. Slightly more complex offset management. Larger reprocessing window. |
| **Simplest setup, lowest guarantee** -- Development, prototyping, or workloads where occasional duplicate processing on restart is acceptable. | `enable.auto.commit=true` (Auto Commit) | Zero application-level commit code. The client commits offsets in the background on a timer. | No commit logic required. Good for getting started quickly. | Offsets may be committed before processing completes (data loss risk on crash). No control over commit timing. Not suitable for production workloads requiring at-least-once guarantees. |

---

## 5. Producer Benchmark Results

### 5.1 Fire-and-Forget (T1.x) -- Maximum Throughput

Uses `Produce` (fire-and-forget) with a delivery handler callback. librdkafka batches internally via `linger.ms`/`batch.size`. A final `Flush(60s)` drains remaining in-flight messages.

- **Avro Small: 28,963 msgs/sec** -- the absolute ceiling for this cluster/client combination.
- **Avro Large: 17,884 msgs/sec** -- Avro binary encoding keeps payloads compact.
- **JSON Large: 6,030 msgs/sec** -- 3x slower than Avro Large due to larger serialized payloads.
- JSON Large achieves the highest *data* throughput at **19.87 MB/sec** (vs Avro Large at 18.83 MB/sec) precisely because each message carries more bytes, even though message count is lower.
- CPU utilization peaks at **~47%** because the serializer and librdkafka run at full speed.

### 5.2 Request-Response (T2.x) -- Per-Message Confirmation

Uses `ProduceAsync` + `await` each message individually. Each message incurs a full network round-trip to the broker.

- Throughput caps at **~7 msgs/sec** regardless of format or payload size.
- Average latency: **~143 ms** per message (network round-trip).
- Format and payload size have **no measurable impact** -- the bottleneck is pure network latency, not serialization.
- CPU barely registers because the producer spends 99% of its time waiting on network I/O.

### 5.3 Batch Processing -- Tight Loop (T3.x)

Uses `ProduceAsync` + `Task.WhenAll` concurrency windows. Messages produced in a tight loop with no inter-message delay. Batch collection uses a 500ms deadline (`BatchTimeoutMs`).

| Window Size | Avg Throughput (msgs/sec) | Avg Latency (ms) | Scaling Factor |
|-------------|--------------------------|-------------------|----------------|
| **1** | ~7 | ~143 | 1x (baseline) |
| **10** | ~68 | ~14.5 | ~10x |
| **100** | ~655 | ~1.5 | ~95x |

**Concurrency windows scale linearly:** each 10x increase in window size yields a proportional ~10x throughput gain, demonstrating that the broker handles concurrent in-flight messages efficiently. Window-1 matches request-response exactly, confirming it as the sequential baseline.

### 5.4 Business-Realistic Batch (T3B.x) -- Inter-Message Delay

Same concurrency window pattern as T3.x, but with a **100ms delay** (`InterMessageDelayMs`) between each `ProduceAsync` call. This simulates real-world applications where messages arrive at application-driven rates rather than in a tight loop.

| Window Size | Avg Throughput (msgs/sec) | Avg Latency (ms) |
|-------------|--------------------------|-------------------|
| **1** | ~7 | ~149 |
| **10** | ~9 | ~109 |
| **100** | ~9 | ~109 |

Window-10 and Window-100 produce **identical results** -- both plateau at ~9 msgs/sec with ~109ms latency.

### 5.5 T3.x vs T3B.x: Tight-Loop vs Realistic Comparison

#### Throughput (msgs/sec averages)

| Format & Size | Window | T3.x (Tight-Loop) | T3B.x (100ms Delay) | Delta |
|---------------|--------|--------------------|----------------------|-------|
| Avro Small | 1 | 7 | 7 | -5% |
| Avro Small | 10 | 70 | 9 | **-87%** |
| Avro Small | 100 | 668 | 9 | **-98.6%** |
| Avro Large | 1 | 7 | 7 | -0.4% |
| Avro Large | 10 | 69 | 9 | **-86.6%** |
| Avro Large | 100 | 656 | 9 | **-98.6%** |
| JSON Small | 1 | 7 | 7 | -2.8% |
| JSON Small | 10 | 68 | 9 | **-86.6%** |
| JSON Small | 100 | 654 | 9 | **-98.6%** |
| JSON Large | 1 | 6 | 7 | +6.9% |
| JSON Large | 10 | 63 | 9 | **-85.4%** |
| JSON Large | 100 | 634 | 9 | **-98.6%** |

#### Latency (avg ms)

| Format & Size | Window | T3.x (Tight-Loop) | T3B.x (100ms Delay) | Change |
|---------------|--------|--------------------|----------------------|--------|
| Avro Small | 1 | 142.1 | 149.4 | +5% |
| Avro Small | 10 | 14.3 | 109.4 | **+665%** |
| Avro Small | 100 | 1.5 | 108.8 | **+7,155%** |
| Avro Large | 1 | 144.0 | 144.7 | +0.5% |
| Avro Large | 10 | 14.6 | 109.0 | **+647%** |
| Avro Large | 100 | 1.5 | 109.0 | **+7,167%** |
| JSON Small | 1 | 147.6 | 151.7 | +2.8% |
| JSON Small | 10 | 14.6 | 108.9 | **+646%** |
| JSON Small | 100 | 1.5 | 108.6 | **+7,140%** |
| JSON Large | 1 | 159.7 | 149.4 | -6.4% |
| JSON Large | 10 | 15.8 | 108.7 | **+588%** |
| JSON Large | 100 | 1.6 | 109.8 | **+6,763%** |

#### Analysis

**Window-1 (sequential produce): No material difference.**
Both tight-loop and realistic tests produce ~7 msgs/sec with ~145--150ms per-message latency. This is expected -- with a window of 1, each message is sent and awaited individually, so the inter-message delay adds no overhead on top of the network round-trip.

**Window-10 and Window-100: Dramatic throughput collapse under realistic conditions.**
- Tight-loop Window-10 delivers 63--70 msgs/sec; with a 100ms inter-message delay it drops to ~9 msgs/sec -- a **7--8x reduction**.
- Tight-loop Window-100 delivers 634--668 msgs/sec; with the delay it drops to the same ~9 msgs/sec -- a **70x reduction**.
- The throughput for Window-10 and Window-100 is **effectively identical** under realistic conditions (~9 msgs/sec), meaning the concurrency window size becomes irrelevant when message arrival rate is the bottleneck.

**Why this happens: The batch deadline fires before the window fills.**
With 100ms between messages and a 500ms batch deadline:
- **Window-1:** 1 message at 100ms -- fills before the 500ms deadline. No change.
- **Window-10:** 10 messages would take 1,000ms -- but the 500ms deadline fires at ~5 messages, flushing a **partial batch of ~5**.
- **Window-100:** 100 messages would take 10,000ms -- the deadline fires at ~5 messages, same partial batch.

This is why Window-10 and Window-100 converge to the same throughput: both are **deadline-driven at ~5 messages per flush**.

**Latency tells the same story from the other side.**
- Tight-loop Window-100: 1.5ms avg latency (messages are batched so densely that they barely wait).
- Realistic Window-100: ~109ms avg latency (each message waits ~half the deadline period before being flushed).
- Realistic Window-10 and Window-100 latencies are identical (~109ms), confirming the deadline is the controlling mechanism.

**Serialization format and payload size have negligible impact.**
Avro vs JSON, small vs large -- the throughput and latency patterns are virtually identical across all formats in the T3B.x tests. The bottleneck is entirely the message arrival rate vs the batch deadline, not serialization overhead.

---

## 6. Consumer Benchmark Results

All consumer tests use manual offset commit (`EnableAutoCommit = false`).

| Strategy | Throughput | Latency per Commit | Description |
|----------|------------|--------------------|-------------|
| **ManualPerMessage** | ~24 msgs/sec | ~41 ms | `consumer.Commit(result)` after every message |
| **ManualBatch** | **~2,064 msgs/sec** | Amortized across 100 messages | `consumer.Commit()` every 100 messages or 500ms |

**Batch commit is 86x faster than per-message commit.**

Per-message commit forces a synchronous broker round-trip after every message -- effectively the consumer equivalent of request-response. At ~41ms per commit, throughput is capped at ~24 msgs/sec regardless of format or payload.

Batch commit amortizes the commit overhead across 100 messages, allowing the consumer to spend most of its time actually consuming rather than waiting on commit acknowledgments.

### Resource Utilization

- **Fire-and-forget** shows the highest CPU usage (up to 47%) because it drives the serializer and librdkafka at full speed.
- **Request-response** barely registers on CPU because it spends 99% of its time waiting on network I/O.
- **Consumer batch commit** uses more memory than per-message (~2 GB vs ~1.4 GB) because it processes messages faster, accumulating more deserialized data in the working set.

---

## 7. Key Observations

1. **Fire-and-forget dominates producer throughput.** Avro Small reached 28,963 msgs/sec -- 4,163x faster than request-response and 42x faster than batch Window-100. `Produce` queues messages into librdkafka's internal buffer and returns immediately; batching, compression, and network I/O happen entirely in the background.

2. **Request-response is the worst for throughput.** Awaiting each `ProduceAsync` individually caps throughput at ~7 msgs/sec. Format and payload size have no measurable impact -- the bottleneck is pure network latency.

3. **Tight-loop concurrency windows scale linearly.** Each 10x increase in window size yields a proportional ~10x throughput gain, demonstrating that the broker handles concurrent in-flight messages efficiently.

4. **Under realistic message arrival rates, concurrency window size becomes irrelevant.** With 100ms inter-message delay, both Window-10 and Window-100 produce identical throughput (~9 msgs/sec) because the 500ms batch deadline fires before the window fills. The effective batch size is determined by `arrival_rate x batch_deadline`, not the window size.

5. **Avro vs JSON: format only matters when the producer is CPU-bound (fire-and-forget).** In the fire-and-forget pattern -- the only pattern where the producer is CPU/serialization-bound rather than network-bound -- Avro Large achieves 17,884 msgs/sec vs JSON Large at 6,030 msgs/sec, a 3x difference driven by Avro's compact binary encoding. However, in request-response and batch concurrency window patterns, the network round-trip latency (~143ms per message) completely dominates execution time, and Avro vs JSON throughput is virtually identical. Serialization format becomes a meaningful performance lever only when network latency is no longer the bottleneck.

6. **Consumer commit strategy is the dominant performance factor.** Batch commit is 86x faster than per-message commit -- the single largest performance lever in the entire benchmark.

---

## 8. Recommendations

1. **Use fire-and-forget for maximum producer throughput** when error handling via delivery handler callbacks is sufficient. At 28,963 msgs/sec, it is orders of magnitude faster than any other pattern.

2. **Use batch `Task.WhenAll(N)` with a time-bound deadline** when per-message error inspection is required. It delivers ~655 msgs/sec (at Window-100) with full `DeliveryResult` checking -- a reasonable middle ground. Throughput scales linearly with window size (10x window = 10x throughput).

3. **Avoid request-response unless strict per-message confirmation is required.** At ~7 msgs/sec, it is 4,000x slower than fire-and-forget. Use only for low-volume, latency-tolerant scenarios where the caller must confirm each message before proceeding.

4. **Right-size the concurrency window for your actual message arrival rate.** Under realistic conditions (100ms inter-message delay), the batch deadline -- not the window size -- controls throughput. Increasing the window beyond `arrival_rate x batch_deadline` yields zero benefit. To increase throughput under realistic conditions:
   - **Increase the batch deadline** -- a longer deadline accumulates more messages per batch, but increases latency.
   - **Increase the message arrival rate** -- if your application naturally produces faster, the concurrency window will fill more before the deadline fires.

5. **Use batch commit (not per-message) for consumers.** The 86x throughput difference is the single largest performance lever in the entire benchmark. A `CommitBatchSize` of 100 is a reasonable starting point; larger values would further amortize commit overhead but increase the reprocessing window on failure.

6. **Prefer Avro over JSON for large payloads in high-throughput fire-and-forget scenarios.** The 3x message throughput advantage (17,884 vs 6,030 msgs/sec) is significant -- but only manifests when the producer is CPU/serialization-bound (fire-and-forget). For request-response and batch concurrency window patterns where network latency dominates, format choice has no measurable throughput impact and can be driven by other factors (schema evolution, tooling preference, developer experience).

7. **For capacity planning, use realistic numbers, not tight-loop numbers.** The T3B.x realistic tests (~9 msgs/sec per producer at 100ms inter-message delay) reflect what applications actually achieve. The T3.x tight-loop numbers (~655 msgs/sec at Window-100) represent the broker and network ceiling, useful for understanding infrastructure limits but not for sizing production workloads.
