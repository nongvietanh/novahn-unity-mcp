# Thiết kế: Console đa-AI trong Unity (Novahn)

> Tài liệu để duyệt **trước khi code**. Mục tiêu: xem các AI giao việc / nói chuyện với nhau
> theo thời gian thực ngay trong Unity, chạy nền khi không mở, nhận nhiều AI cùng lúc, và
> giao diện riêng (bỏ look CoplayDev).

## 0. Hiện trạng (đã đọc code)

| Thành phần | File | Ghi chú |
|---|---|---|
| Lưu trữ task/message | [`Server/src/hub/store.py`](../Server/src/hub/store.py) | SQLite. Bảng `tasks`, `messages`, `audit_events`. **Nội dung (`request`, `result`, `body`) đã lưu sẵn.** |
| Agent cứng = 2 | `store.py:12` `AGENTS = {"codex","claude"}` | Chặn ở `create_task` và `send_message` |
| Cấu hình agent cứng | [`Server/src/hub/config.py`](../Server/src/hub/config.py) | `codex_port/claude_port/*_token` là field cố định |
| Wiring server | [`Server/src/hub/__main__.py`](../Server/src/hub/__main__.py) | Vòng lặp hardcode `(bridge, codex, claude)`, mỗi agent 1 port + 1 token + 1 uvicorn |
| Endpoint MCP mỗi agent | [`Server/src/hub/endpoint.py`](../Server/src/hub/endpoint.py) | `hub.task.*`, `hub.message.*` qua FastMCP, có auth Bearer + loopback |
| Route metadata | [`Server/src/hub/bridge_endpoint.py`](../Server/src/hub/bridge_endpoint.py) | `GET /hub/status` (loopback, không cần token) |
| Snapshot metadata | [`Server/src/hub/dashboard.py`](../Server/src/hub/dashboard.py) | **Cố tình bỏ nội dung** |
| Window Unity (chỉ metadata) | [`MCPForUnity/Editor/Windows/MultiAgentHubWindow.cs`](../MCPForUnity/Editor/Windows/MultiAgentHubWindow.cs) | Poll `/hub/status` mỗi 3s, chỉ hiện số đếm. Ghi rõ *"bodies never enter the Editor"* |
| Window chính (look CoplayDev) | [`MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.cs`](../MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.cs) | 1130 dòng, tab Connect/Tools/… |

**Quyết định cần bạn chấp thuận:** tính năng này **đảo lại** nguyên tắc cũ "nội dung không vào Editor".
Vì đây là máy local của bạn nên hợp lý, nhưng ghi rõ ở đây để duyệt có ý thức.

---

## 1. Giai đoạn 1 — Console AI trong Unity (tính năng lõi)

### 1.1. Backend: endpoint transcript

**Con trỏ (cursor) ổn định — không cần migration schema.**
`audit_events` ghi mọi sự kiện (`task.created`, `task.claimed`, `task.completed`, `task.cancelled`,
`message.sent`, `message.ack`…). SQLite tự có `rowid` tăng đơn điệu theo thứ tự insert, nên dùng
`rowid` làm cursor: *"cho tôi sự kiện có rowid > cursor"*. Không đụng vào schema hiện có.

**Thêm `HubStore.transcript(after_rowid, limit)`** (store.py):
```sql
SELECT rowid, correlation_id, actor, kind, object_id, created_at
FROM audit_events
WHERE rowid > :after_rowid
ORDER BY rowid
LIMIT :limit            -- mặc định 200, trần 500
```
Với mỗi sự kiện, join lấy nội dung theo `kind`:
- `task.*`  → lấy `request`, `result`, `assignee`, `creator`, `state` từ `tasks` theo `object_id`
- `message.*` → lấy `sender`, `recipient`, `body`, `task_id` từ `messages` theo `object_id`

Trả về:
```json
{
  "events": [
    {"seq": 42, "at": 1760000000.0, "actor": "codex", "kind": "task.created",
     "taskId": "…", "assignee": "claude", "text": "Tạo GameObject Player…", "state": "queued"},
    {"seq": 43, "at": 1760000005.0, "actor": "claude", "kind": "message.sent",
     "recipient": "codex", "text": "Đã xong, xem scene Main", "taskId": "…"}
  ],
  "nextCursor": 43,
  "agents": ["codex", "claude"]
}
```

**Thêm route `GET /hub/transcript`** trong `BridgeOnlyEndpoint` (bridge_endpoint.py), **ngay cạnh
`/hub/status`**, cùng chuẩn: loopback-only, chỉ GET, không cần token (server chỉ nghe 127.0.0.1).
Tham số: `?after=<cursor>&limit=<n>`. Dùng lại cơ chế `self.dashboard`-style (truyền một callable
`transcript` vào `BridgeOnlyEndpoint` từ `__main__.py`, giống `dashboard=dashboard.snapshot`).

> Bảo vệ kích thước: cắt `text` ở ~2 KB/sự kiện phía server để tránh kéo nội dung khổng lồ vào Editor.

### 1.2. Unity: `AgentConsoleWindow`

File mới: `MCPForUnity/Editor/Windows/AgentConsoleWindow.cs` (+ `.uxml` + `.uss`).
Thay thế vai trò xem của `MultiAgentHubWindow` (giữ lại hay xoá — xem §4).

**Hành vi:**
- Menu: `MCP For Unity > AI Console` (phím tắt gợi ý `Ctrl+Shift+J`).
- Khi **mở**: poll `GET /hub/transcript?after=<lastCursor>` mỗi ~1.5s qua `UnityWebRequest`
  (đúng pattern `EditorApplication.update` + throttle như `MultiAgentHubWindow` hiện tại).
  Giữ `lastCursor`, chỉ nối dòng mới → không vẽ lại toàn bộ.
- Khi **đóng**: ngừng poll. **Agent vẫn chạy nền trên server** — console chỉ là bộ xem, không
  điều khiển vòng đời agent. Mở lại thì nó kéo tiếp từ cursor đã lưu (hoặc tải lại toàn bộ gần đây).

**Giao diện console (cuộn, log-style):**
```
[14:03:21]  codex  ──task──▶  claude    "Tạo prefab Enemy với Rigidbody"      #a1b2c3
[14:03:28]  claude ✓ done                "Đã tạo Assets/Enemy.prefab"          #a1b2c3
[14:03:30]  claude ──msg───▶  codex      "Cần bạn review collider"
```
- Mỗi agent 1 màu ổn định (hash từ agentId → màu), nhãn in đậm.
- Dòng có: thời gian, actor, loại (task/msg + mũi tên tới đích), snippet nội dung, mã task rút gọn.
- Thanh công cụ trên: **Lọc theo agent** (dropdown), **Pause/Resume**, **Clear**, **Auto-scroll** (toggle),
  nhãn trạng thái Hub + giờ cập nhật cuối.
- Dùng `ListView` ảo hoá (virtualized) để log dài vẫn mượt; trần ~2000 dòng, cũ nhất rơi dần.

**Không chạm** tới cơ chế agent giao việc — console chỉ đọc. Các AI tự giao task qua MCP tool
`hub.task.create` như hiện tại; chạy nền hay không là do client AI, không phải console.

### 1.3. Test G1
- Python (`Server/tests/`): `test_hub_transcript.py` — tạo vài task/message, kiểm transcript trả đúng
  thứ tự, cursor tăng, cắt nội dung, lọc. Test route `/hub/transcript` (method, loopback, tham số).
- Unity (`TestProjects/UnityMCPTests/Assets/Tests/`): test parse JSON transcript → model dòng console,
  logic merge theo cursor (không trùng dòng), ánh xạ màu theo agentId.

---

## 2. Giai đoạn 2 — Nhiều AI động (bỏ cứng 2 con)

### 2.1. Mô hình cấu hình mới (`config.py`)
Thay 4 field cứng bằng **danh sách agent**. Định dạng env đề xuất (giữ đơn giản, ASCII):
```
UNITY_HUB_AGENTS=codex:4317,claude:4318,gemini:4319
UNITY_HUB_TOKEN_CODEX=<≥32 ký tự>
UNITY_HUB_TOKEN_CLAUDE=<…>
UNITY_HUB_TOKEN_GEMINI=<…>
```
- `HubConfig` giữ `agents: tuple[AgentSpec, ...]` với `AgentSpec(id, port, token)`.
- `validate()` tổng quát hoá: mọi port (gồm bridge) phải phân biệt & hợp lệ; mọi token ≥32 ký tự,
  ASCII không khoảng trắng, **đôi một khác nhau**; id khớp `^[a-z0-9_-]{1,32}$`.
- **Tương thích ngược:** nếu thấy `UNITY_HUB_CODEX_*`/`UNITY_HUB_CLAUDE_*` kiểu cũ và không có
  `UNITY_HUB_AGENTS`, tự dựng danh sách `[codex, claude]` như trước → không vỡ setup hiện tại.

### 2.2. Store động (`store.py`)
- Bỏ hằng `AGENTS = {"codex","claude"}`. `HubStore.__init__(path, agents: set[str])` nhận tập agent hợp lệ.
- `create_task`/`send_message` kiểm `assignee`/`recipient` theo tập này (giữ nguyên lỗi
  `invalid_assignee` / lỗi recipient).

### 2.3. Wiring (`__main__.py`) + dashboard
- Thay tuple cứng `(bridge, codex, claude)` bằng vòng lặp trên `config.agents` + 1 entry `bridge`.
  Cơ chế "mỗi agent 1 socket + 1 uvicorn + 1 endpoint" **đã tổng quát**, chỉ cần đổi nguồn danh sách.
- `HubDashboard` nhận `agents` động thay cho `codex_port/claude_port`; `last_seen` dựng từ danh sách.
- `/hub/status` và `/hub/transcript` tự liệt kê mọi agent đang cấu hình.

### 2.4. Test G2
- `test_hub_config.py`: parse `UNITY_HUB_AGENTS`, validate port/token, tương thích ngược env cũ.
- `test_store.py`: task/message hợp lệ với agent thứ 3; từ chối agent lạ.
- Console (G1) hiển thị đúng khi có ≥3 agent (màu, lọc).

---

## 3. Giai đoạn 3 — Giao diện riêng (bỏ look CoplayDev)

Thuần thẩm mỹ, rủi ro thấp nhất → làm **sau cùng**.
- Thay logo OceanMark (inject ở `CreateGUI` index 0) bằng asset logo của bạn; đổi tiêu đề
  "MCP for Unity" → tên sản phẩm của bạn; đổi `version-label`.
- Viết lại bảng màu/typography trong `MCPForUnity/Editor/Windows/Components/Common.uss` và các `.uss`
  của window. **Giữ nguyên cấu trúc tab & logic**, chỉ restyle.
- Đồng bộ phong cách console (G1) với skin mới.
- (Tuỳ chọn) Rà soát chuỗi/branding còn sót trong README package & `ProductInfo`.

### Test G3
- Chạy `tools/check-unity-versions.sh` compile đa phiên bản; mở window mắt thường kiểm layout
  (uss không làm vỡ). Không có logic mới nên chủ yếu smoke-test.

---

## 4. Câu hỏi còn mở để bạn chốt khi duyệt
1. **`MultiAgentHubWindow` cũ**: xoá hẳn (theo CLAUDE.md "delete rather than deprecate") hay giữ làm
   tab "Metadata" bên trong console mới? → đề xuất: **xoá**, gộp mọi thứ vào `AgentConsoleWindow`.
2. **Giữ nội dung lịch sử bao lâu**: transcript đọc từ `audit_events` (không tự xoá). Có cần nút
   "Clear history" ghi xoá DB, hay chỉ clear phía view? → đề xuất: clear chỉ ở view; xoá DB để sau.
3. **Định dạng env đa-agent**: dùng `UNITY_HUB_AGENTS=id:port,…` + token rời như §2.1, hay 1 biến JSON?
   → đề xuất: dạng `id:port` + token rời (dễ gõ, hợp validate hiện có).
4. **Phạm vi đợt này**: làm tuần tự G1 → G2 → G3, hay chỉ G1 trước rồi dừng để bạn xem thực tế?

## 5. Thứ tự thực thi đề xuất
`G1 (backend transcript → Unity console → test)` → duyệt thực tế → `G2` → `G3`.
Mỗi giai đoạn 1 commit/PR riêng trên nhánh tách từ `beta` (theo CLAUDE.md), kèm test xanh.

**Trạng thái:** G1, G2, G3 đã xong và merge vào `main`.

---

# Giai đoạn 4 — Setup nhiều AI qua UI + chọn leader (B1)

> Mục tiêu: trong cửa sổ Novahn, đăng ký nhiều AI cùng lúc (mỗi con là 1 agent
> riêng có port + token), chọn 1 con làm **leader (điều phối)**, và bật/tắt
> multi-agent hub ngay từ Editor. Quyết định đã chốt: transport mới **"Multi-Agent
> Hub"**; phạm vi **B1** (chưa làm Coordinator skill).

## 4.0. Giới hạn quan trọng của B1 (đọc kỹ)
B1 làm xong **hạ tầng**: nhiều agent + bus chia task + chỉ định leader + console xem.
Nhưng **leader CHƯA tự động lên plan & chia task** — đó là việc của LLM, cần
**Coordinator skill** (dạy leader dùng `hub.task.create` để fan-out). Skill đó là
**đợt sau (B2)**. Sau B1, bạn vẫn điều phối được nhưng phải **tự bảo leader** "chia
task cho các agent X, Y qua hub"; nó chưa tự làm. B1 = cắm điện sẵn, B2 = bật công tắc tự động.

## 4.1. Mô hình dữ liệu (Editor)
`HubAgentRegistry` lưu danh sách agent (JSON trong `ProjectSettings/` hoặc EditorPrefs):
mỗi agent `{ id, displayName, clientKey, port, token, isLeader }`.
- `id`: khớp `^[a-z0-9_-]{1,32}$` (dùng cho env + bus).
- `port`: tự cấp phát từ 4317 tăng dần, tránh trùng bridge (8080) và nhau.
- `token`: tự sinh 48 ký tự URL-safe mỗi agent.
- Đúng **một** agent `isLeader = true`.

## 4.2. Backend — bật/tắt hub từ Editor
`ServerManagementService` thêm đường chạy `python -m hub` (song song đường server đơn
hiện có), truyền env từ registry:
```
UNITY_HUB_BRIDGE_URL=http://127.0.0.1:8080
UNITY_HUB_AGENTS=<id1>:<port1>,<id2>:<port2>,...
UNITY_HUB_TOKEN_<ID>=<token>   # mỗi agent một dòng
```
Hub tự dựng bridge (:8080 /hub/plugin) + từng agent endpoint; **WebSocket bridge của
Unity nối tới /hub/plugin y như chế độ HTTP hiện tại** (không đổi phía Unity transport).
Stop = kết thúc tiến trình hub.

## 4.3. UI — chế độ "Multi-Agent Hub"
- Dropdown **Transport** thêm mục `Multi-Agent Hub` (cạnh HTTP Local / stdio).
- Chọn nó → hiện panel **Agents** mới (`McpHubAgentsSection`):
  - Mỗi dòng agent: `displayName` · dropdown client (Codex/Claude Code/Cursor…) · `port`
    · radio **Leader** · nút **Remove**.
  - Nút **Add Agent**, **Configure All Agents**, **Start/Stop Hub**.
  - **Configure All Agents**: với mỗi dòng, chạy configurator của client đó nhưng trỏ vào
    **port + token của chính agent đó** (header `Authorization: Bearer <token>`), thay vì
    server chung :8080.
- Console G1 (`AI Console`) hiển thị luồng task leader→worker ngay.

## 4.4. Thay đổi code chính
- `ConfigJsonBuilder`: thêm overload ghi **url (port agent) + bearer token** thay cho url
  chung. B1 nhắm các client **hỗ trợ HTTP + header** (Codex, Claude Code, Cursor); client
  chỉ-stdio tạm chưa hỗ trợ gán port riêng.
- `ServerManagementService`: `StartHub()/StopHub()` với env ở 4.2.
- Mới: `HubAgentRegistry` (model + lưu) và `McpHubAgentsSection` (UI).
- Transport enum/handling: thêm `MultiAgentHub`.

## 4.5. Test
- EditMode: cấp phát port không trùng; sinh token; ràng buộc đúng 1 leader; dựng chuỗi
  env `UNITY_HUB_AGENTS` + các dòng token; JSON cấu hình mỗi agent (url+bearer đúng).
- Python: đã phủ parse config đa-agent ở G2.
- Compile-check đa nền tảng; mở Unity kiểm bằng mắt (UI + start/stop hub).

## 4.6. Rủi ro
- Mỗi client có định dạng config/bearer khác nhau → B1 giới hạn client HTTP; mở rộng sau.
- Chạy `python -m hub` cần đúng uv/PYTHONPATH như đường server hiện tại (phức tạp vừa).
- **Kỳ vọng "leader tự plan"**: không có trong B1 (cần B2 — Coordinator skill).
