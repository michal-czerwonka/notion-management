Używaj nazwy funkcjonalności i jawnie podawaj etap. Przykładowy flow:

```text
Start business discovery for feature: Task XP

Chcę naliczać XP za ukończone zadania...
```

Po rozmowie:

```text
Finish business discovery for Task XP.
Update the feature document and show me any remaining blocking questions.
```

Następnie, w nowym czacie:

```text
Start technical design and implementation planning for feature: Task XP.
Read the existing feature document first.
```

Po akceptacji planu:

```text
Finish technical design for Task XP.
Update the feature document with confirmed decisions and the implementation plan.
```

Implementacja:

```text
Implement feature: Task XP.
Read the feature document first and follow the approved implementation plan.
```

Review na branchu funkcjonalności:

```text
Start feature review and readiness review for: Task XP.
Compare the current branch with main and update the feature document.
```

Gdy chcesz wrócić do wcześniejszego etapu:

```text
Continue business discovery for: Task XP.
```

albo:

```text
Continue technical design for: Task XP.
```

Jeśli nie jest jasne, na jakim etapie jesteś:

```text
Show the current status of feature: Task XP.
Read its feature document and tell me the recommended next step.
```
