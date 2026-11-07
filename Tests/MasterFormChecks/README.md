# Master form checks

Run from the repository root:

```powershell
dotnet run --project Tests/MasterFormChecks/MasterFormChecks.csproj --artifacts-path "$env:TEMP/ibs-rcd-master-validation" -- "$env:TEMP/ibs-rcd-rendered-masters"
npm install --prefix "$env:TEMP/ibs-rcd-form-check-deps" jsdom jquery
$env:JSDOM_PATH = "$env:TEMP/ibs-rcd-form-check-deps/node_modules/jsdom"
$env:JQUERY_PATH = "$env:TEMP/ibs-rcd-form-check-deps/node_modules/jquery"
node Tests/MasterFormChecks/check.cjs "$env:TEMP/ibs-rcd-rendered-masters"
```

The C# runner renders the actual compiled Razor views with sample edit values, without connecting to the production database. The JavaScript checks exercise the rendered controls and production scripts using jsdom. These checks do not replace a visual browser review.

Source forms: IBSWeb Areas/Filpride/Views/{Customer,Supplier,BankAccount,Service} and Areas/User/Views/Product. IBSWeb has no separate ChartOfAccount Create/Edit views; RCD retains its account hierarchy workflow with the shared form stylesheet and corrected cascade behavior.

RCD adaptations: Documented-only customers, retained active/branch/multiple-term controls, RCD commissionee and account lookups, supplier rates in percentage points, optional customer Business Style, and working supplier document download actions. Inactive Supplier sections are disabled so hidden required fields cannot block submission. Saved conditional values are retained when opening Edit.
