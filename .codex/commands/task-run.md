---
description: Execute exactly one Atlas Tester delivery task
---

Execute exactly one task from `docs/tasks/tasks.md`.

Optional argument: `$ARGUMENTS`

Accept no argument or one exact task ID such as `SCN-JSON-01`. With no argument, select the first `planned` row whose dependencies are `done`. Never select a different task when an explicit ID was supplied.

Follow this workflow completely:

1. Read `docs/tasks/tasks.md` and the selected task specification.
2. Confirm the current Git branch and status. Never discard, reset, clean, stash, or overwrite unrelated user work.
3. If the task is `done`, or a dependency is unfinished, report that and stop.
4. Before implementation, close all running `Atlas.Tester.Wpf` instances.
5. Change only the selected task row from `planned` to `in_progress`.
6. Implement only the selected task and its acceptance criteria; do not start a later task.
7. Run focused tests and build `Atlas.Tester.Wpf`. Fix in-scope failures.
8. Review the complete diff, including JSON compatibility, saved-client-data behavior, and unrelated changes.
9. Change only the selected task row from `in_progress` to `review`.
10. Launch the WPF executable in a visible window. Report the changed behavior, test/build evidence, and a concise manual checklist. Stop for owner approval before a commit.
11. After explicit owner acceptance only, change the selected task row from `review` to `done`, stage task-related files, commit, and push `main` without force.
12. Report the commit hash and verification evidence, then stop. Never begin the next task in the same run.
