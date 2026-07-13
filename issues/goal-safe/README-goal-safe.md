# Goal-safe GestureClip execution

Use this when `/goal @issues/gesture-optimization.csv` hits context-window limits.

Run from a fresh Codex thread in `C:\Users\Hp\Desktop\loenggg`:

1. Send `hello` first.
2. Then run:

```text
/goal @C:\Users\Hp\Desktop\loenggg\issues\goal-safe\00-runner.csv
```

If it still hits context limit, run phase files one by one in fresh threads:

```text
/goal @C:\Users\Hp\Desktop\loenggg\issues\goal-safe\01-baseline-tests.csv
/goal @C:\Users\Hp\Desktop\loenggg\issues\goal-safe\02-gesture-hotkey-core.csv
/goal @C:\Users\Hp\Desktop\loenggg\issues\goal-safe\03-ux-performance-diagnostics.csv
/goal @C:\Users\Hp\Desktop\loenggg\issues\goal-safe\04-regression-release-integration.csv
/goal @C:\Users\Hp\Desktop\loenggg\issues\goal-safe\05-niuma-assistant-roadmap.csv
```

Context rules for the runner: do not paste raw logs over 80 lines into chat; summarize command output; write exact bulky evidence into files when needed; persist progress in CSV states/notes after every row.
