# Repository Guidelines

## Project Structure & Module Organization

This repository is a React/Vite single-page application:

- `react-app/src/` contains routes, views, the direct HiThink API client, local watchlist storage, utilities, and UI primitives.
- `react-app/public/` contains static assets and deployment metadata.
- `docs/external-api/` contains the HiThink A-share, index, fund, and metadata API reference.
- `README.md` describes application behavior and data integration.

## Build, Test, and Development Commands

Run commands from `react-app/`:

```bash
npm ci                 # Install the locked dependency set
npm run dev            # Start the Vite development server
npm run build          # Type-check and create the live frontend build
npm run lint           # Run ESLint
npm run format         # Format src/ with Prettier
npm test               # Run Vitest
```

Set `VITE_HITHINK_API_KEY` in `.env` before using live data. Vite embeds `VITE_*` values in the browser build by design.

## Coding Style & Naming Conventions

For TypeScript, use two spaces, single quotes, semicolons, and a 120-character print width. Run Prettier and ESLint. Use PascalCase for types, camelCase for values, and preserve the existing DTO property names when adapting API responses. Prefer semantic Tailwind tokens and existing UI components.

Keep HiThink request and response adaptation in `react-app/src/api/WatchlistManager.ts`, and preserve the DTO shape consumed by `ExampleView.tsx`.

## Testing Guidelines

Frontend tests use Vitest with `.test.ts` or `.test.tsx` suffixes. Add focused regression coverage for API parsing, range handling, local storage, and drawdown calculations. Run relevant tests and the production build when practical.

## Commit & Pull Request Guidelines

Recent commits are generated `CodeBuddy Studio WIP [...]` entries, so no formal human convention is established. Use concise imperative messages, optionally scoped by area, for example `fix(api): handle missing market data`.

Pull requests should explain the change, affected paths, verification commands, and screenshots or recordings for UI changes. Call out external API or configuration changes.

## Security & Configuration

The HiThink key is intentionally exposed in the client bundle for this personal-use deployment. Do not paste it into source files or commit it in a public repository. Keep it in local/build environment configuration and rotate it if abused.
