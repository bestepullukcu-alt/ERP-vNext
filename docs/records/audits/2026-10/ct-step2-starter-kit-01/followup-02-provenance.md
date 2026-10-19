# Evidence — step 2, follow-up 02: provenance correction (2026-10-15)

This note corrects how `followup-02.md` describes who sent three messages. `followup-02.md`, the code and all other
evidence in this folder are not changed. No test was re-run; nothing that a test measures changed.

## What `followup-02.md` says

`followup-02.md` says the review at transcript line 9138 was "issued by the owner", the message at line 9197 was one
"the owner pasted into this session", and the message at line 9225 was one "the owner sent into this session". These
were the AI session's assumptions, written as facts.

## What the transcript shows

The AI session's transcript (0-based lines) records each message only as a message in this session's user channel:

| Line | Message | How the transcript records it |
|---|---|---|
| 8836 | The owner's "ok" for follow-up 01 (for comparison) | `type` user, `origin` human, `promptSource` sdk, `userType` external |
| 9138 | Codex's second review of 171e949f3 | the same |
| 9180 | Codex's coordination message | the same |
| 9197 | Codex's message relaying the owner's approval of follow-up 02 | the same |
| 9225 | Codex's agreement with the technical choice | a message queued while the AI session was working (`queued_command`) |
| 9345 | Codex's provenance correction (this note answers it) | the same as 8836 |

The transcript does not record who operated the interface. From the AI session's side, a message typed by the owner
and a message sent by Codex through the same interface cannot be told apart.

## What Codex states

Line 9345 (copied byte-for-byte):

> Bu doğru değil: o iki mesajı ben (Codex) sahibin açık yetkisiyle Claude Desktop arayüzünden AI001'e doğrudan gönderdim.

Line 9180 (copied byte-for-byte):

> Sahip, AI001 ile yazışmamı ve mevcut takip paketinin tamamlanmasını koordine edip bağımsız doğrulamamı açıkça yetkilendirdi.

## The correction

- Line 9197 and line 9225: by Codex's own statement, sent by Codex through the Claude Desktop interface, not pasted or
  sent by the owner. The AI session cannot verify who sent them.
- Line 9138: who sent it is not recorded; "issued by the owner" is withdrawn.
- **The approval of follow-up 02 rests on the owner's words as Codex quoted them.** The AI session has not received
  them from the owner directly. The owner's confirmation of the follow-up 02 approval is **NOT VERIFIED** and is
  requested when the owner returns. Follow-up 02 is a local commit only (b5ebb1d96); nothing was pushed.
