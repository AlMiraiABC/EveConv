---
applyTo: "**/*.cs,**/*.csproj"
---
# Test Rules

## Unit Tests

- All tests should be located in the `Tests/` folder.
- Use xUnit as the testing framework.
- Each project should has a corresponding test project, e.g., `EveConv.Abstraction.Tests`.
- Tests should support CI/CD integration.
- Tests should be named clearly to indicate their purpose.
- Tests projects name should follow the pattern: `EveConv.{ProjectName}.Tests`.
- Tests files should be organized to mirror the structure of the source project.
- Tests names should follow the Arrange-Act-Assert (AAA): `MethodName_StateUnderTest_ExpectedBehavior`.
- Use mocking frameworks as needed to isolate units under test.
- For model related projects, include accuracy validation tests comparing results with Python implementations where applicable. The python implementations should be included in `Tests/Python/` folder.
- Tests should support cross-platform execution (at least Windows and Linux).
- Ensure tests are idempotent and do not rely on external state.
- Tests should only depend on one project being tested.
- You can mark project's classes or methods to `internal` visibility and use `<InternalsVisibleTo Include="$(AssemblyName).Tests" />` in `csproj` to allow test project to access them.
- Remember to clean up any resources created during tests to avoid side effects.
- Add `TestContext.Current.CancellationToken` for async tests if it needs cancelation support.

## Integration Tests

- Integration tests should be placed in a separate folder, e.g., `Tests/Integration/`.
- Use xUnit as the testing framework.
- Integration tests should cover interactions between multiple components.
- Tests should be named clearly to indicate their purpose. Add readme file or comments to explain the integration scenarios if needed.
- Integration tests should support CI/CD integration.
- Tests names should follow the pattern: `BusinessScenario_ExpectedBehavior`.
- Use real or mocked services as needed to simulate interactions.
- Integration tests should support cross-platform execution (at least Windows and Linux).

## Notes

- Using `xunit.v3` as test framework.
- Avoid using older xunit versions (v2.x) unless necessary for legacy projects.
- Test projects package managed by NuGet CPM(Central Package Management). All dependencies should be defined in Directory.Packages.props.
