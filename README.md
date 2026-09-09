# JJX 基金、股票、指数回测监控

这是一个供个人使用的 React 单页面应用，用于搜索基金、ETF、股票和指数，查看不同时间区间的历史回撤曲线。

## 技术栈

- React + TypeScript + Vite
- Tailwind CSS + ShadCN UI primitives
- 同花顺 HiThink Financial API
- `localStorage` 本地保存监控列表

项目现在是纯前端结构，已移除 `Services/` 后端。页面从浏览器直接请求同花顺接口，在客户端完成数据适配和最大回撤计算。

## 本地运行

在 `react-app/` 目录执行：

```bash
npm ci
npm run dev
```

构建、检查和测试：

```bash
npm run build
npm run check-ts
npm run lint
npm test
```

在 `react-app/.env` 中配置：

```dotenv
VITE_HITHINK_API_KEY=your-api-key
```

由于 Vite 会将 `VITE_*` 变量打包进浏览器，API Key 会对访问网站的人可见。这是本项目个人自用部署的明确取舍，不要将真实 key 写入公开 Git 提交。

## 数据流程

1. 使用 `/api/meta/tickers/search` 搜索并确认唯一 `thscode`。
2. 根据资产类型请求股票、指数快照/历史 K 线或基金净值。
3. 在 `react-app/src/api/WatchlistManager.ts` 统一响应字段并计算最大回撤。
4. 将监控摘要保存到浏览器 `localStorage`；不同浏览器或设备之间不会同步。

同花顺接口说明位于 `docs/external-api/`。前端主页面位于 `react-app/src/views/ExampleView.tsx`。
