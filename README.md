# Recipes

A small restaurant back-office app: recipes and menus, goods receipts, stock, sales, and the printable
documents that go with them (invoices, goods receipts, consumption and production reports).

Built with ASP.NET Core Razor Pages on .NET 10, Entity Framework Core and SQLite.

## A very old project, modernized

This is a very old project that I found in an old archive of mine. It started life as an
**ASP.NET 2.0** web application running on **Windows XP**, was later moved to **ASP.NET MVC 3 / .NET 4.0**.

I brought it back to life and modernized it, keeping what made it good (the workflow, the calculations and its
look) and replacing what time had broken. See [What changed](#what-changed) below.

## What it does

The app follows the daily life of a small kitchen:

1. **Goods receipts** – record what arrived from suppliers: quantities, pack sizes, purchase prices, a selling
   price, and a **VAT rate per line** (invoices often mix rates). Totals follow the lines and can be corrected by
   hand to match the paper invoice.
2. **Recipes** – a recipe is a list of products with quantities. Its cost per portion comes from the latest
   goods receipt prices, and it works out a selling price from a markup and a packaging cost.
3. **Menus** – menus group recipes and can be valid on a given day, which makes a weekly menu.
4. **Sales** – ring up menus, recipes or products, with meal vouchers, and optionally print a fiscal receipt and
   an invoice. Each sale line keeps the VAT rate that applied at the time.
5. **Inventory** – the theoretical stock is what was received minus what the sales consumed. A stock count
   compares it with what is really on the shelves, and the difference becomes a printable write-off.
6. **Reports** – stock balance by day or month, consumption, sales summary, beverage management and production
   reports, all printable.

## Pages

| Area | Pages |
|---|---|
| **Sales** (`/sales`, the start page) | Sales of the day with a calendar · New sale · Sale details · Invoice · Reports: Stock balance, Consumption report, Sales summary, Beverage management report, Production report |
| **Menus** (`/menus`) | Menus of the day · Edit menu (add recipes, clone) · Weekly menu and its printout |
| **Recipes** (`/recipes`) | Filterable list with price changes · Edit recipe (ingredients, cost, markup, VAT) · Print recipe |
| **Goods receipts** (`/goods-receipts`) | Receipts of the day · Edit receipt (supplier, lines, VAT per line, create a missing product on the spot) · Print receipt with price-change report |
| **Inventory** (`/inventory`) | Current theoretical stock · New product · Stock count (with new products and write-offs) · Replace or delete a product · Print inventory |

The menu column on every page has an **Activities** section (what you can do here) and **Other places**
(shortcuts to related pages). Forms that need a product or recipe that doesn't exist yet link to creating it.

## Architecture

A single project, no layers for the sake of layers.

| Path | What |
|---|---|
| `Pages/<Area>` | Razor Pages: a `.cshtml` and its page model. Each page declares its own route, e.g. `@page "/recipes/edit/{id:int?}"` |
| `Pages/Shared` | `_Layout` (menu, notices), `_Print` (layout of the printable documents), `_Calendar`, `_OtherPlaces` |
| `Pages/Search.cshtml.cs` | JSON lookups used by the autocomplete fields |
| `Services` | `ReportService` (stock and consumption calculations), `CashRegister` (fiscal receipts), settings |
| `Models`, `Data`, `Migrations` | One file per entity, the EF Core `RecipesContext`, query extensions, migrations |
| `Infrastructure` | A model binder that accepts decimals typed with a comma or a dot |
| `wwwroot` | `css/app.css` and `css/print.css`, `js/site.js`, SVG icons and images |
| `Tests` | Integration tests (see below) |

Some choices worth knowing:

- **Server-rendered.** Forms that build a list (sale, recipe, menu, receipt, stock count) post back for every
  action (`?handler=Add`, `Remove`, ...) and render again, so there is no client-side state. The small
  `site.js` only adds autocomplete, Enter-to-add-a-line and confirmations; the pages also work without it.
- **Documents are pages.** Invoices, reports and receipts are ordinary pages with the `_Print` layout that open
  the browser's print dialog.
- **Money and VAT.** Recipes, menus, products and goods receipt lines each carry their own VAT rate, and a sale
  line stores the rate it was sold with, so changing a rate later never rewrites history. No rate is hardcoded.
- **Time is injected** (`TimeProvider`), which is what lets the tests run on a fixed date.

## What changed

| | Then | Now |
|---|---|---|
| Runtime | ASP.NET 2.0 on Windows XP, later MVC 3 / .NET 4.0 | ASP.NET Core Razor Pages on .NET 10, runs anywhere |
| Data | the original data layer | EF Core with SQLite, migrations, created on first run |
| Language | Romanian: UI, code, database | English: UI, code, database, tests |
| Code | Windows-era conventions | One file per entity, page models, services; modern C# (file-scoped namespaces, primary constructors, collection expressions, `required`, `field`, extension blocks, source-generated JSON and regex) |
| Tests | – | Integration tests on a temporary database, with a fixed clock |
| Looks | the original look | the same look and palette, made responsive with modern CSS |
| VAT | one rate for a whole document | a rate per line |

The original look was kept on purpose: the flower in the corner, the textured background, the blue-grey
menu tabs. It was only polished, with a responsive layout, a calendar and icons.

## Run

```bash
dotnet run
```

The SQLite database is created and migrated on startup at `App_Data/recipes.db`
(change it in `appsettings.json` → `ConnectionStrings:Recipes`).

## Tests

```bash
dotnet test
```

The tests run the app on a temporary SQLite database and post forms like a browser, antiforgery included.
They only build in Debug, so a Release build carries no test code or packages.

## Configuration

`appsettings.json` holds:

- the company, delegate and invoice issuer details printed on invoices (`Company`, `Delegate`, `InvoiceIssuer`);
- the name printed under "Storekeeper" on reports;
- `StandardVat`: only the *starting* rate offered for new products, recipes, menus and receipt lines, since each
  of them has its own rate;
- `FPrint`: the Datecs fiscal printer settings.

### Fiscal printer

The fiscal receipt writes a file in the Datecs FPrint format and starts `FPrint.exe`. It only works when the
app runs on the Windows machine the printer is attached to, and it is skipped (with a message) everywhere
else. I no longer have a fiscal printer, so this part is **untested** since the move to .NET 10, and the lines
sent to the printer still use a single tax group.

## Database changes

```bash
dotnet tool restore
dotnet ef migrations add <Name>
```
