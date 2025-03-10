# Contributing

Thank you for your interest in contributing to *glTFast*! We are
incredibly excited to see how members of our community will use and extend
*glTFast*. To facilitate your contributions, we've outlined a brief set
of guidelines to ensure that your extensions can be easily integrated.

## Communication

Please read through our [code of conduct][COC], as we expect all our contributors to follow it.

## Contact

For inquiries of all sorts there are ways to get in touch with the *glTFast* maintainers.

- Create an [issue on GitHub][NewIssue] (preferred way for bug reports and feature requests)
- Post a reply in the [glTFast announcement post][Announcement] on [Unity Discussions][Discussions].

## Contribution Ideas

If you're looking for ideas on ways to contribute browse the [issues][issues],
especially ones with the *help wanted* or *good first issue* label.

## Preparation

Before starting to work on a contribution we recommend searching within the
existing [issues][issues] and [pull requests][pulls] for similar topics to
avoid redundant efforts and make sure you got all contextual information.

Feel free to propose ideas upfront via an [issue][NewIssue] that briefly outlines
your intended changes. We'll then try to give you advice and feedback on how to
optimally implement those changes or, if justifiable, reasons to abandon an
idea. This pre-evaluation can raise the chances of getting a contribution
accepted.

## Version Control

*glTFast* uses [Git][Git] and [GitHub][repo] for version control.

## Submission via Pull Request

Changes can be proposed via [pull requests (PR)][GithubDocPR] on the [pull requests][pulls] page.

In order to get a positive review and increase the chances getting a PR merged, make sure the PR has the following traits.

- Concise title.
- Detailed description of the proposed improvements.
- Testing
  - All existing [tests](tests.md) pass.
  - Added or modified code is covered by tests. Add new tests if needed.
- [Changelog][changelog] entry.
- Updates and additions to documentation, if applicable.
- References to issues that the PR resolves, if any.
- [Contributor License Agreements](#contributor-license-agreements) signed by all authors.

PRs will be transferred to an internal, mirrored repository to undergo review and (automated) tests that use Unity&reg; internal tools and infrastructure. If those tests and reviews are not successful, we'll try to help you resolving remaining issues. Once all problems are resolved, the actual merge will happen on the mirrored repository and the original pull requests will get closed with a proper notification about expected release version and date.

## Contributor License Agreements

When you open a pull request, you will be asked to acknowledge our [Contributor
License Agreement][GltfastCLA]. You will have to confirm that your Contributions are your
original creation and that you have complete right and authority to make your
contributions. We allow both individual contributions and contributions made on
behalf of companies.

## Trademarks

*Unity&reg;* is a registered trademark of [Unity Technologies][unity].

[Announcement]: https://discussions.unity.com/t/unity-gltfast-package-is-now-available/935685
[changelog]: https://keepachangelog.com/en/1.0.0/
[COC]: code-of-conduct.md
[Discussions]: https://discussions.unity.com/
[Git]: https://git-scm.com/
[GltfastCLA]: https://cla-assistant.cds.internal.unity3d.com/Unity-Technologies/com.unity.cloud.gltfast
[repo]: https://github.com/Unity-Technologies/com.unity.cloud.gltfast
[issues]: https://github.com/Unity-Technologies/com.unity.cloud.gltfast/issues
[NewIssue]: https://github.com/Unity-Technologies/com.unity.cloud.gltfast/issues/new/choose
[pulls]: https://github.com/Unity-Technologies/com.unity.cloud.gltfast/pulls
[GithubDocPR]: https://docs.github.com/pull-requests
[unity]: https://unity.com
