# 宇尘加速器

基于 SNI 代理 + DNS 优化的本地游戏加速工具，**无需任何外部服务器**，保障数据安全。

## 功能

- **Steam 加速** — 商店、社区、工坊、下载
- **GitHub 加速** — 网页、克隆、下载、Release
- **游戏加速** — 内置 SOCKS5 代理，支持 CS2、Dota 2、Apex、PUBG、GTA V 等 20+ 主流游戏
- **系统代理** — 自动配置 Windows 系统代理
- **TCP 优化** — 启用 CTCP 拥塞控制，优化高延迟连接
- **流量监控** — 实时图表 + 速度统计
- **托盘运行** — 最小化到系统托盘，后台静默加速

## 运行要求

- Windows 10 / 11
- .NET 8 Desktop Runtime（框架依赖模式）或无需安装（自包含模式）

## 技术原理

```
浏览器/游戏 → 系统代理/SOCKS5 → SNI 代理(本机) → 最优 CDN IP → 目标服务器
                                      ↑
                              DNS 多源并发解析
                         (Google/Cloudflare/AliDNS/114DNS)
```

1. **SNI 代理** — 解析 TLS ClientHello 中的 SNI 域名，将流量转发到延迟最低的 CDN 节点
2. **DNS 优化** — 并发查询 7 个公共 DNS 服务器，合并结果并 TCP 测速选最优 IP
3. **规则引擎** — 内置 Steam/GitHub/主流游戏域名规则，自动匹配加速策略

## 开发构建

```bash
dotnet build
```

## 项目结构

```
src/
├── GameAccelerator.Core/       # 核心库
│   ├── Configuration/          # 配置管理
│   ├── Dns/                    # DNS 优化器
│   ├── Hosts/                  # Hosts 文件管理
│   ├── Network/                # 系统代理、TCP 优化、流量统计
│   ├── Proxy/                  # SNI 代理服务器
│   ├── Rules/                  # 规则引擎 + 内置规则
│   └── Socks5/                 # SOCKS5 代理服务器
├── GameAccelerator.UI/         # WPF 桌面界面
│   ├── Services/               # 加速服务
│   └── ViewModels/             # MVVM 视图模型
└── scripts/                    # 构建脚本
```

## 许可

MIT
