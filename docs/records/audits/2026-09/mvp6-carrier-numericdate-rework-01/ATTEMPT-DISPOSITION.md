# Attempt disposition

The first candidate HTTP/profiler run returned 45/49. HTTP behavior was correct, including all three string NumericDate rejections, but Mongo profiler counts were inconsistent in four cases: the valid request was observed as zero reads, while three rejected duplicate cases were observed as one read. The raw output was emitted to the task terminal but was not captured to a file, so it is not presented as durable raw evidence.

The probe was changed only to wait 150 ms after the HTTP response before reading the profiler. The same 49 scenarios then passed 49/49. This successor run is controlling candidate evidence. The first run remains a failed evidence-capture attempt and is not called PASS.
