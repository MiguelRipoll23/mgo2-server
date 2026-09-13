# AGENTS.md

* Each domain owns its own service(s).
* A domain must never interact directly with another domain.
* To use another domain’s functionality, call its service.
* Keep domain boundaries strict and avoid cross-domain dependencies.
* No source file may exceed 400 LOC.
* Organize code into focused services, interfaces, utils, types, etc. to keep files below the 400 LOC limit.
* Services and utilities must use the service or utils suffix respectively in both the file name and class name.
* Do not use abbreviations unless they are extremely common and well-established, such as id, utils, or info.
