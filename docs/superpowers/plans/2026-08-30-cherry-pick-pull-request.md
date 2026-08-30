# Cherry-Pick Pull Request Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a repository page directly below Stashes that loads a GitHub or Azure DevOps pull request and cherry-picks one or all of its commits onto the currently checked-out branch.

**Architecture:** Keep pull-request discovery provider-neutral at the UI layer. `PullRequestRemote` detects GitHub/Azure remotes and describes their pull refs; `FetchPullRequest` fetches those refs with the repository's existing Git authentication path; the page resolves ordered PR commits and hands them to the existing `CherryPick` popup. The repository view is extended from a separate partial class so the large upstream Repository XAML does not need invasive editing.

**Tech Stack:** .NET 10, Avalonia 11, CommunityToolkit.Mvvm, existing DevBoard Git command layer, xUnit.

**Spec:** Approved conversation design: Cherry Pick PR below Stashes; GitHub + Azure DevOps; current branch target; oldest-to-newest; clean-worktree preflight; reuse Continue/Abort conflict flow; reject merge commits in v1.

## Global Constraints

- Support GitHub and Azure DevOps Git remotes in v1.
- Reuse DevBoard's existing Git authentication and `CherryPick` popup.
- Cherry-pick onto the currently checked-out branch.
- Preserve oldest-to-newest commit order.
- Reject PR commit sets containing merge commits.
- Do not silently choose a merge mainline.
- Keep provider-specific ref logic out of the view.

---

### Task 1: Pull-request remote detection and ref mapping

**Files:**
- Create: `src/Models/PullRequestRemote.cs`
- Test: `tests/DevBoard.Tests/PullRequestRemoteTests.cs`

**Interfaces:**
- Produces: `PullRequestRemote.TryCreate(Remote remote, int pullRequestNumber, out PullRequestRemote descriptor)`.
- Produces: provider kind plus merge/head remote refs and stable local refs.

- [ ] **Step 1: Write failing tests** for GitHub HTTPS/SSH, Azure `dev.azure.com`/`visualstudio.com`, unsupported hosts, invalid PR numbers, and provider-specific pull refs.
- [ ] **Step 2: Verify RED** in CI/build because the new type does not exist yet.
- [ ] **Step 3: Implement the minimal descriptor** using the existing `Remote.TryGetVisitURL` normalization.
- [ ] **Step 4: Verify GREEN** for the descriptor tests.
- [ ] **Step 5: Commit** the implementation.

### Task 2: Fetch and resolve ordered PR commits

**Files:**
- Create: `src/Commands/FetchPullRequest.cs`
- Create: `src/ViewModels/PullRequestCherryPickPage.cs`
- Test: `tests/DevBoard.Tests/PullRequestCommitRangeTests.cs`

**Interfaces:**
- Consumes: `PullRequestRemote` from Task 1.
- Produces: `PullRequestCherryPickPage.LoadAsync()` and an oldest-to-newest `Commits` collection.

- [ ] **Step 1: Write failing tests** for merge-ref ranges (`merge^1..merge^2`), GitHub head fallback ranges, and merge-commit rejection helper behavior.
- [ ] **Step 2: Verify RED** in CI/build.
- [ ] **Step 3: Add `FetchPullRequest`** with the same SSH/PAT credential resolution behavior as normal fetch.
- [ ] **Step 4: Implement load flow**: validate remote/PR number, fetch merge ref, GitHub head fallback when needed, query commits with `--reverse`, reject merge commits, expose status/error state.
- [ ] **Step 5: Verify GREEN** for focused tests and solution build.
- [ ] **Step 6: Commit** the implementation.

### Task 3: Repository UI and cherry-pick actions

**Files:**
- Create: `src/Views/PullRequestCherryPickPage.axaml`
- Create: `src/Views/PullRequestCherryPickPage.axaml.cs`
- Create: `src/Views/Repository.PullRequests.cs`

**Interfaces:**
- Consumes: `PullRequestCherryPickPage` view model from Task 2.
- Produces: fourth repository navigation item directly after Stashes and a right-side PR page.

- [ ] **Step 1: Add the page UI** with remote selector, PR number input, Load button, commit list, per-row cherry action, and Cherry-pick all button.
- [ ] **Step 2: Extend the repository view** from a separate partial file and append the navigation item directly after Stashes.
- [ ] **Step 3: Add preflight** for current branch, clean working copy, and no operation already in progress.
- [ ] **Step 4: Route one/all actions** into the existing `CherryPick` popup so existing conflict Continue/Abort handling remains authoritative.
- [ ] **Step 5: Build on all CI platforms** and fix Avalonia/XAML/compiler issues.
- [ ] **Step 6: Commit** the UI.

### Task 4: Final verification and PR

**Files:**
- Modify only files required by failures found during verification.

**Interfaces:**
- Consumes: all prior tasks.
- Produces: review-ready PR.

- [ ] **Step 1: Run/observe full PR CI** including multi-platform build and formatting checks.
- [ ] **Step 2: Inspect the complete diff** for scope, provider behavior, and accidental changes.
- [ ] **Step 3: Verify no merge commits can be silently cherry-picked** and commit order remains oldest-to-newest.
- [ ] **Step 4: Request code review** using the Superpowers review workflow.
- [ ] **Step 5: Leave the PR open for the user's one-time review.**
