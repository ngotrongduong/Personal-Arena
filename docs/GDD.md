# Thiết kế game: chế độ Survivor

> Tài liệu thiết kế (GDD) cho hướng đi mới từ M4. Owner chốt hướng ngày 2026-09-30 (D-026 đến D-029).
> Bản góp ý thiết kế của owner được áp dụng cùng ngày (D-030).
> Mọi con số là **mức khởi điểm**; sẽ cân bằng lại khi xem AI chơi. Đổi số thì sửa ở đây và ở data trong Core.

**Bản sắc:** *Build the warrior. Train the brain. Watch it learn.*

## 1. Ý tưởng

Game giống **Vampire Survivors** (VS), nhưng người điều khiển nhân vật là **AI tự học**. Người chơi không
bấm gì trong trận. Họ làm "huấn luyện viên và người lên build":

1. Bắt đầu với **1 Warrior**. Bấm TRAIN để AI của nó tự học chơi chế độ Survivor.
2. Xem AI đánh các trận 15 phút. AI tự nhặt EXP, tự chọn nâng cấp, tự farm vàng.
3. Vàng AI mang về dùng để:
   - nâng **cấp nhân vật**; mỗi cấp được 1 điểm chỉ số, người chơi tự cộng theo ý (nhiều máu, chí mạng…);
   - mua **class** khác (Mage, Archer);
   - mở **bậc độ khó** cao hơn, nơi rớt nhiều vàng hơn.
4. Đổi build thì bấm TRAIN tiếp. **AI học lối đánh hợp với build đó**, nên mỗi người chơi có một AI riêng.

Vòng lặp chính: **Train → Xem / Farm → Nhận vàng → Lên build → Train tiếp**.

**Nguyên tắc quan trọng nhất:** người chơi phải **cảm nhận được AI đang học**. Vì vậy:
- có **Behavior Profile** (mục 4.7): các chỉ số hành vi đo từ dữ liệu thật, không bịa;
- có **báo cáo sau trận** kể lại trận đấu (mục 5);
- **não tốt nhất không bao giờ bị mất** (champion/challenger, mục 4.6).

## 2. Một trận (in-run)

### 2.1 Bản đồ

- Bãi nghĩa địa **100 × 100 m**, có tường rào bao quanh (không đi xuyên được).
- Khoảng **40 vật cản** hình tròn (bia mộ, cây chết, tảng đá), bán kính 0,5–1,5 m, đặt theo seed.
  Không đặt vật cản trong vòng 6 m quanh điểm xuất phát (giữa bản đồ).
- Không còn vực; không ai rơi chết.
- Camera đi theo nhân vật, nhìn nghiêng từ trên xuống. Vẫn xoay và zoom được như trước.

### 2.2 Thời gian, thắng và thua

- Đồng hồ đếm **từ 0:00 tới 15:00** (thời gian trong game).
- **Phút 15:** boss **Chúa tể xương** xuất hiện. Quái thường ngừng sinh thêm, trừ đệ tử boss gọi ra.
- **Thắng:** hạ boss. Được thưởng vàng lớn (500 × hệ số vàng của bậc).
- **Thua:** máu về 0. Ghi lại **nguyên nhân chết**: bị vây (≥ 6 quái chạm người), boss, Brute, quái chạm.
- **Hết hạn:** tới **17:00** vẫn chưa hạ được boss thì trận kết thúc, không thắng không thua.
  (Bỏ Thần chết: nó chỉ dạy AI một điều vô ích là "không tránh được".)
- Thắng hay thua đều **giữ lại vàng đã nhặt**, giống VS.

### 2.3 Nhân vật trong trận

- AI điều khiển:
  - **di chuyển** 8 hướng hoặc đứng yên;
  - **skill chủ động** (tối đa 4 ô, Warrior dùng 3);
  - **chọn nâng cấp** khi lên cấp.
- Hướng nhìn tự quay về phía quái gần nhất, không còn nhánh "xoay".
- **Vũ khí tự động** tự bắn khi hết hồi chiêu, nhắm quái gần nhất hoặc bắn quanh người, tùy vũ khí.
- Chỉ số cơ bản của Warrior:

| Chỉ số | Giá trị |
|---|---|
| Máu tối đa | 150 |
| Hồi máu | 0,2 máu/giây |
| Giáp | 0 (trừ thẳng vào mỗi đòn, tối thiểu còn 1 sát thương) |
| Tốc chạy | 4,5 m/s |
| Bán kính nhặt đồ | 1,5 m |
| Chí mạng | 5%, sát thương chí mạng 150% |
| Năng lượng | 100, hồi 15/giây (dùng cho khiên và lướt) |

- **Vũ khí khởi đầu:** Kiếm quét cấp 1.

### 2.4 Skill chủ động của Warrior (AI tự bấm)

Dùng lại từ M3:

| Skill | Tác dụng |
|---|---|
| **Đá** | Đẩy lùi mạnh và choáng 1,2 s các quái phía trước; hồi 3 s |
| **Khiên** | Giữ để chặn đòn và đạn phía trước (sát thương ×0,5, parry thì choáng quái); tốn năng lượng |
| **Lướt** | Lao 3 m với tốc độ 18 m/s để thoát vòng vây; hồi 2,5 s |

### 2.5 Nguyên tắc: mỗi món tạo ra một **lối đánh**

Vũ khí và chỉ số không chỉ tăng số. Mỗi món phải **buộc AI chơi khác đi**, để người xem thấy được AI học
lối đánh hợp với build:

| Món | Lối đánh AI nên học |
|---|---|
| Kiếm quét | Đứng gần, quay mặt về đám quái phía trước |
| Búa ném | Chạy vòng tròn lớn, thả diều (kite) |
| Hào quang | Đứng **trong** đám quái |
| Giáo đâm | Giữ tầm trung, dồn quái thành hàng |
| Rìu xoay + tốc chạy | Chạy lướt qua rìa đám quái |
| Sóng chấn động + hồi chiêu | Gom quái rồi nổ một lần |
| Nam châm + Tham lam | Chọn đường nguy hiểm hơn để lấy tài nguyên |

**Cơ chế nối chiêu:** tỉ lệ chí mạng **×2 với quái đang choáng**. Build chí mạng vì vậy sẽ dùng Đá nhiều hơn.

### 2.6 Vũ khí tự động của Warrior (6 loại, mang tối đa **4**)

Mỗi vũ khí có **cấp 1–5**. Lên cấp tăng số liệu như trong bảng. Cột "Có từ" cho biết món nào vào game ở
giai đoạn nào (mục 6).

| Vũ khí | Cách đánh | Cấp 1 | Mỗi cấp thêm | Có từ |
|---|---|---|---|---|
| **Kiếm quét** | Chém vòng cung 120° về phía quái gần nhất, tầm 2,5 m | 20 ST, hồi 1,2 s | +8 ST, +10% vùng; cấp 5 chém 2 phía | M4A |
| **Búa ném** | Ném búa vào quái ngẫu nhiên trong 10 m, xuyên 1 con | 25 ST, hồi 1,5 s | +7 ST; cấp 2 và 4 thêm 1 búa | M4A |
| **Giáo đâm** | Đâm thẳng 5 m, xuyên mọi quái trên đường | 30 ST, hồi 1,8 s | +10 ST; cấp 3 và 5 thêm 1 mũi | M4C |
| **Rìu xoay** | Rìu bay vòng quanh người, bán kính 2,2 m, tồn tại 4 s | 12 ST/lần chạm, hồi 3 s | +4 ST; cấp 2 và 4 thêm 1 rìu | M4C |
| **Hào quang** | Vòng sát thương liên tục quanh người, bán kính 1,8 m, đẩy nhẹ | 5 ST mỗi 0,4 s | +2 ST, +10% vùng | M4C |
| **Sóng chấn động** | Vòng sóng lan ra tới 5 m, đẩy lùi | 18 ST, hồi 4 s | +6 ST, −0,3 s hồi | M4C |

### 2.7 Phụ kiện (8 loại, mang tối đa **4**, cấp 1–5)

| Phụ kiện | Mỗi cấp | Có từ |
|---|---|---|
| **Tim sắt** | +10% máu tối đa | M4A |
| **Giáp xương** | +1 giáp | M4A |
| **Mắt chí mạng** | +4% tỉ lệ chí mạng | M4A |
| **Ủng gió** | +8% tốc chạy | M4A |
| **Găng sức mạnh** | +8% sát thương | M4C |
| **Đồng hồ cát** | −6% hồi chiêu vũ khí | M4C |
| **Bùa vùng** | +8% vùng ảnh hưởng | M4C |
| **Nam châm** | +25% bán kính nhặt đồ | M4C |

### 2.8 EXP và lên cấp trong trận

- Quái chết rớt **ngọc EXP**. Giá trị ngọc bằng EXP của quái:
  - Walker 1, Runner 1, Spitter 2, Brute 5;
  - tinh anh 50, boss thì không rớt.
- Ngọc có 3 màu theo giá trị: **xanh dương** < 10, **xanh lá** 10–99, **đỏ** ≥ 100.
- Khi trên bản đồ có hơn **400 ngọc**, ngọc mới dồn vào ngọc gần nhất để game không chậm.
- **Đường EXP (giống VS):**
  - cấp 2 cần 5 EXP;
  - từ cấp 3 tới cấp 20, mỗi cấp cần thêm 10 so với cấp trước;
  - từ cấp 21 tới 40, mỗi cấp cần thêm 13; từ cấp 41, mỗi cấp cần thêm 16;
  - lên cấp 20 cần thêm 600 và lên cấp 40 cần thêm 2.400 (bước ngoặt giống VS).
- **Lên cấp:**
  - game **dừng lại** và hiện **3 lựa chọn**, không trùng nhau;
  - May mắn cho thêm cơ hội có **lựa chọn thứ 4**, xác suất = May mắn / (100 + May mắn);
  - lựa chọn lấy từ:
    - vũ khí hoặc phụ kiện mới, nếu còn ô trống;
    - nâng cấp món đang có, nếu món đó chưa tới cấp 5.
- Khi không còn gì để nâng, lựa chọn đổi thành **"+25 vàng"** hoặc **"hồi 30 máu"**.
- Lên nhiều cấp cùng lúc thì lần lượt chọn từng cấp.
- **AI là bên chọn.** Trình xem hiện bảng lựa chọn khoảng 1 giây và **tô sáng món AI chọn**.

### 2.9 Quái

Dùng 4 loại zombie của M3, nhưng chỉnh số liệu hợp với kiểu "đông quái". Thêm tinh anh và boss.

| Loại | Máu phút 0 | Tốc | Cách đánh | EXP | Có từ |
|---|---|---|---|---|---|
| Walker | 15 | 2,0 | Chạm người: 8 ST, mỗi con gây ST tối đa 1 lần / 0,5 s | 1 | M4A |
| Runner | 10 | 4,0 | Chạm người: 5 ST | 1 | M4A |
| Brute | 80 | 1,6 | Vung đòn có báo trước 0,9 s: 25 ST; kháng hất lùi 60% | 5 | M4A |
| Spitter | 20 | 2,4 | Giữ khoảng cách 7 m, nhổ đạn 10 ST (khiên chặn được) | 2 | M4C |

- Máu quái tăng **+12% mỗi phút**; sát thương tăng **+5% mỗi phút**.
- **Tinh anh:** ở các phút 3, 6, 9 và 12, sinh một tinh anh thuộc loại quái chính của đoạn đó.
  Tinh anh to gấp 1,6 lần, máu ×10.
  - M4A: rớt **một túi vàng lớn** (20–40 vàng) và **một ngọc 50 EXP**.
  - M4C: rớt thêm **rương**.
- **Boss Chúa tể xương** (phút 15):
  - dáng Brute nhưng to gấp 2,5 lần, máu 6.000, chậm 1,4 m/s;
  - cứ 6 s vung đòn quét 40 ST, có báo trước 1,2 s;
  - cứ 10 s gọi 8 Walker.

### 2.10 Lịch sinh quái (bậc 1)

- Quái sinh ngẫu nhiên trên vòng **18–24 m quanh nhân vật**, ngoài tầm camera.
- Không sinh trong tường hay trong vật cản.

| Phút | M4A: Walker, Runner, Brute (trọng số) | M4C: thêm Spitter | Tối đa cùng lúc | Nhịp sinh |
|---|---|---|---|---|
| 0–1 | 1, 0, 0 | 0 | 30 | 1 con / 1 s |
| 1–3 | 3, 1, 0 | 0 | 60 | 2 / s |
| 3–5 | 3, 2, 1 | 0 | 90 | 3 / s |
| 5–7 | 2, 2, 1 | 1 | 120 | 4 / s |
| 7–10 | 2, 3, 1 | 2 | 160 | 5 / s |
| 10–12 | 1, 3, 2 | 2 | 200 | 6 / s |
| 12–15 | 1, 3, 3 | 3 | 250 | 8 / s |

- Quái cách nhân vật hơn 40 m sẽ được "dời" lên vòng sinh phía trước nhân vật, như VS.
  Nhờ vậy nhân vật chạy xa cũng không bỏ lại cả đám.

### 2.11 Đồ nhặt

| Đồ | Nguồn | Tác dụng | Có từ |
|---|---|---|---|
| Ngọc EXP | Mọi quái | Cộng EXP (× Học nhanh) | M4A |
| **Xu vàng** | Quái thường 3% (× May mắn); tinh anh luôn rớt túi lớn | 1–5 vàng × hệ số bậc × Tham lam | M4A |
| Thịt | Quái 0,5% | Hồi 30 máu | M4A |
| Nam châm | Quái 0,2% | Hút mọi ngọc trên bản đồ về người | M4C |
| Rương | Tinh anh | Nâng ngẫu nhiên 1 món đang có (lên 1 cấp) + 50–150 vàng × hệ số bậc | M4C |

- Nhân vật tự hút đồ trong **bán kính nhặt**. Nam châm và phụ kiện Nam châm tăng bán kính này.

## 3. Ngoài trận (meta)

### 3.1 Vàng

- Vàng nhặt trong **trận thật** được cộng vào ví khi trận kết thúc. Trận thật gồm:
  - **Xem trận:** owner xem AI đánh 1 trận, tốc độ ×1 tới ×4;
  - **Farm tự động:** cho AI đánh N trận ẩn ở tốc độ tối đa, xong báo tổng vàng.
- **Trận huấn luyện không cho vàng**, vì hàng nghìn trận chạy song song, cho vàng thì kinh tế vô nghĩa.
- **Đo trước, cân bằng sau:** mọi trận thật ghi telemetry kinh tế (vàng/phút, EXP/phút, nguồn vàng,
  bậc, build). Giá cấp nhân vật, giá class và hệ số vàng chỉ được chỉnh sau khi có số liệu này.

### 3.2 Cấp nhân vật và điểm chỉ số

- Mỗi nhân vật có **cấp** (khác cấp trong trận). Lên cấp bằng vàng. Giá = ⌊100 × 1,25^cấp hiện tại⌋:
  cấp 0→1 giá 100, cấp 9→10 khoảng 745, cấp 19→20 khoảng 6.939.
- Mỗi cấp cho **1 điểm chỉ số**. Người chơi cộng điểm vào 13 chỉ số dưới đây.
- **Tẩy điểm** miễn phí, để thử build thoải mái.

| Chỉ số | Mỗi điểm | Tối đa điểm |
|---|---|---|
| Máu tối đa | +5% | 20 |
| Giáp | +0,5 | 10 |
| Hồi máu | +0,1 máu/s | 10 |
| Sức mạnh | +3% sát thương | 20 |
| Chí mạng | +2% tỉ lệ | 20 |
| ST chí mạng | +10% | 20 |
| Hồi chiêu | −2% | 15 |
| Vùng | +3% | 15 |
| Tốc chạy | +2% | 10 |
| Nam châm | +10% bán kính nhặt | 10 |
| May mắn | +5 | 10 |
| Tham lam | +5% vàng | 20 |
| Học nhanh | +3% EXP | 10 |

- **Ví dụ build:**
  - "Trâu": Máu, Giáp, Hồi máu. AI nên học cách lao vào giữa đám quái.
  - "Chí mạng": Chí mạng, ST chí mạng, Sức mạnh. Nhân vật giòn hơn, AI nên học cách giữ khoảng cách,
    né nhiều hơn và Đá để làm choáng (chí mạng ×2 với quái choáng).
  - "Farm": Tham lam, May mắn, Nam châm. Đánh yếu hơn nhưng nhiều vàng.

### 3.3 Build Loadout (M5)

- Mỗi nhân vật có **5 ô loadout**. Mỗi ô lưu một bộ điểm chỉ số có tên (ví dụ "Trâu", "Chí mạng").
- Đổi loadout tức thì, không mất vàng.
- Mỗi loadout **ghi thành tích riêng**: số trận, thời gian sống trung vị, vàng/phút, tỉ lệ hạ boss.
- Màn **so sánh loadout** đặt các số này cạnh nhau, để owner thấy build nào hợp với AI hiện tại.

### 3.4 Training Focus (M5)

Trước khi bấm TRAIN, owner chọn **trọng tâm huấn luyện**. Mỗi trọng tâm là một bộ hệ số thưởng khác nhau
(vẫn giữ thứ bậc ở mục 4.3, chỉ đổi độ nặng):

| Trọng tâm | Ý nghĩa |
|---|---|
| Cân bằng | Mặc định |
| Sống sót | Phạt mất máu nặng hơn |
| Vàng | Thưởng vàng cao hơn |
| Boss | Thưởng gây sát thương lên boss cao hơn |
| Tấn công | Thưởng tiến độ EXP cao hơn, chấp nhận mất máu |

### 3.5 Bậc độ khó

- Có **10 bậc**. Thắng bậc N (hạ boss) thì mở bậc N+1. Người chơi chọn bậc trước mỗi trận.
- Mỗi bậc vừa **tăng số** vừa thêm **modifier** (luật chơi khác đi), để bậc cao cần lối đánh khác chứ
  không chỉ "trâu hơn".

| Hệ số ở bậc N | Công thức | Bậc 1 | Bậc 5 | Bậc 10 |
|---|---|---|---|---|
| Máu quái | 1 + 0,35·(N−1) | ×1 | ×2,4 | ×4,15 |
| Sát thương quái | 1 + 0,2·(N−1) | ×1 | ×1,8 | ×2,8 |
| Nhịp sinh / tối đa cùng lúc | 1 + 0,15·(N−1) | ×1 | ×1,6 | ×2,35 |
| **Vàng** | 1 + 0,5·(N−1) | ×1 | ×3 | ×5,5 |

Modifier khởi điểm (cộng dồn, chốt ở M5):

| Bậc | Modifier |
|---|---|
| 2 | Quái dày hơn 10% |
| 3 | Tinh anh xuất hiện thêm ở phút 1,5 |
| 4 | Runner nhanh hơn 15% |
| 5 | Thịt rớt ít đi một nửa |
| 6 | Brute xuất hiện sớm từ phút 1 |
| 7 | Tinh anh gấp đôi |
| 8 | Quái hồi máu chậm khi không bị đánh |
| 9 | Boss có đòn gọi thêm Runner |
| 10 | **Nightmare:** tất cả những điều trên, quái chết để lại vũng độc ngắn |

### 3.6 Class và nhân vật

- Người chơi mới có **Warrior**.
- **Mage** giá 1.500 vàng, **Archer** giá 3.000 vàng (M7). Mỗi class có bộ vũ khí, phụ kiện và
  skill chủ động riêng.
- Mỗi class người chơi sở hữu là **một nhân vật** có cấp, điểm chỉ số và **não AI riêng**.

### 3.7 Lưu tiến trình

- File `profile.json` trong thư mục dữ liệu của game (`Application.persistentDataPath`).
- Chỉ lưu trên máy, không gửi đi đâu (D-007).
- Gồm:
  - vàng;
  - các nhân vật (class, cấp, điểm chỉ số, 5 loadout, đường dẫn não riêng);
  - bậc đã mở;
  - thống kê (số trận, trận thắng, vàng đã farm, thời gian sống lâu nhất).

## 4. Não AI

### 4.1 Hành động (3 nhánh)

| Nhánh | Lựa chọn |
|---|---|
| Di chuyển | 9 (đứng yên + 8 hướng) |
| Skill chủ động | 5 (không làm gì + 4 ô) |
| Chọn nâng cấp | 5 (không chọn + 4 lựa chọn) |

**Khóa hành động chặt (action mask):** AI không bao giờ được chọn hành động vô nghĩa.
- Ô skill chưa có, đang hồi chiêu hoặc thiếu năng lượng bị khóa.
- Khi bảng lên cấp mở: chỉ được chọn một trong các lựa chọn đang hiện; di chuyển và skill bị khóa.
- Khi không có bảng lên cấp: nhánh chọn nâng cấp chỉ còn "không chọn".

Mạng dùng **một bộ mã hóa chung** (3 lớp × 512) và **một đầu ra riêng cho mỗi nhánh**; ML-Agents đã làm
đúng như vậy. AI quyết định 12 lần/giây như trước. Khi bảng lên cấp mở, sim dừng cho tới khi AI chọn.

### 4.2 Quan sát (schema v4): **2.264 số**, đã chuẩn hóa, **chừa chỗ** cho nội dung sau này

- **Mọi số nằm trong [−1, 1]**, chuẩn hóa ngay trong schema (chia cho mức tối đa hợp lý rồi kẹp lại),
  nên không cần bộ chuẩn hóa của ML-Agents và nâng cấp não dễ hơn.
- Có **đặc trưng chuyển động**, để AI thấy quái đang lao tới hay đang lùi, không chỉ thấy vị trí:

| Khối | Số lượng | Nội dung |
|---|---|---|
| Bản thân | 64 | Máu %, máu tối đa, năng lượng, **4 ô hồi chiêu** và 4 cờ sẵn sàng, đang khiên, đang lướt, vận tốc, khoảng cách tới 4 bức tường, thời gian trận, cấp, tiến độ EXP, bậc, cờ "đang chọn nâng cấp", boss (có không, hướng, khoảng cách, máu), **16 ô chỉ số build** (13 dùng), số quái đang sống, vàng trong trận; **hành động di chuyển và skill ở lượt trước**; hướng nhìn |
| Túi đồ | 64 | Cấp / 5 của từng món trong **danh mục 64 món** |
| Bảng lên cấp | 4 × 66 | Mỗi lựa chọn: one-hot 64 món + cấp kế tiếp + cờ hợp lệ |
| Tia | 72 × 25 | Mỗi tia có 2 phần. **Vật cứng** (17): loại (không có, tường/vật cản, **8 loại quái**, đạn địch, 1 chỗ trống), khoảng cách, 3 cờ (tinh anh, đang vung đòn, choáng) và **tốc độ tiến lại gần**. **Đồ nhặt** (8, nhìn xuyên quái): loại (không có, ngọc, vàng, thịt, rương, nam châm, 1 chỗ trống) + khoảng cách |
| Mật độ | 72 | 8 hướng × 3 vòng (0–5, 5–12, 12–30 m) × (số quái, lượng EXP, **tốc độ trung bình đám quái tiến lại gần**) |

Chi tiết từng ô nằm ở `docs/tasks/T-014-survivor-core-sim.md`.

Thêm quái mới (tối đa 8 loại), thêm món mới (tối đa 64) hay thêm chỉ số (tối đa 16) mà **không đổi kích
thước**, nên não **học tiếp** được ngay.

### 4.3 Thưởng: **kết quả quan trọng hơn hoạt động**

Thứ bậc: **thắng/thua > sống sót > giữ máu > tiến độ > vàng**. Không thưởng cho việc hạ quái, vì AI sẽ
học cách "cày số" thay vì sống sót. Mỗi khoản có test dấu, và có test "kết quả luôn thắng thế".

| Sự kiện | Thưởng |
|---|---|
| **Thắng** (hạ boss) | +10 |
| Hết thời gian của bài học ngắn (curriculum) | +5 |
| **Chết** | −5 |
| Sống thêm 1 giây | +0,01 |
| Mất máu | −1 × (máu mất / máu tối đa) |
| Tiến độ lên cấp | +0,05 cho mỗi cấp (tính theo phần trăm thanh EXP) |
| Nhặt 1 vàng | +0,001 |
| Gây sát thương lên boss | +2 × (phần máu boss bị mất) |

### 4.4 Học theo build và não riêng (M4B, M5)

- **Não nền mỗi class** học với build **ngẫu nhiên**: cấp nhân vật 0–50, điểm rải ngẫu nhiên,
  bậc 1–10. Nhờ vậy não biết đánh với mọi build.
- **Não riêng của nhân vật** khởi tạo từ não nền. Sau đó học tiếp với khoảng 70% trận dùng **đúng build
  của người chơi** (dao động nhẹ) và 30% build ngẫu nhiên.
- Khi game cập nhật:
  - Kích thước quan sát không đổi: **học tiếp** từ checkpoint.
  - Kích thước lớn hơn: công cụ `Trainer/brain_upgrade.py` **nối thêm** trọng số. Đầu vào mới có trọng
    số 0, nên não mới cho kết quả y hệt não cũ, rồi học tiếp.

### 4.5 Curriculum huấn luyện

- Trận ngắn trước, dài dần: 3 → 6 → 10 → 15 phút. Lên bài khi **điểm thưởng trung bình** đủ cao
  (tức là AI sống hết bài hiện tại đủ thường xuyên).
- M4B thêm bậc độ khó và build ngẫu nhiên tăng dần.
- **Curriculum ôn tập (M4B):** khi thêm nội dung mới, trận huấn luyện chia **70% nội dung mới, 20% nội
  dung cũ, 10% tình huống khó** (bậc cao, bị vây), để não không quên cái đã biết.

### 4.6 Đánh giá và champion/challenger

- **Bộ đánh giá:** cho não chơi **100 seed chưa từng gặp** khi huấn luyện, ở bậc 1, không dừng giữa chừng.
  Ghi mỗi trận: thời gian sống, cách kết thúc, nguyên nhân chết, cấp, vàng, EXP, máu mất, sát thương lên
  boss. Tổng hợp: trung vị, P10 (10% trận tệ nhất), vàng/phút, EXP/phút, máu mất/phút, tỉ lệ hạ boss.
- **Nghiệm thu M4A:** trung vị thời gian sống ≥ **10:00**, P10 ≥ **7:00**, **không trận nào chết trước
  3:00** (thất bại thảm).
- **Champion/challenger (M4B):**
  - não tốt nhất hiện tại là **champion**, não vừa train là **challenger**;
  - challenger chỉ thay champion khi **thắng trên bộ đánh giá**;
  - **không bao giờ ghi đè não tốt nhất**: champion cũ luôn được giữ lại, quay về được.

### 4.7 Behavior Profile (M4B)

Các chỉ số hành vi đo từ dữ liệu thật của trận (telemetry), không đặt tay:

| Chỉ số | Cách đo |
|---|---|
| Hung hăng | Tần suất di chuyển về phía đám đông |
| Thận trọng | Khoảng cách giữ với quái; có lùi lại khi máu thấp không |
| Tham | Tỉ lệ đồ nhặt được so với đồ rớt ra |
| Khám phá | Số ô bản đồ đã đi qua |
| Khống chế đám đông | Skill trúng ≥ 3 quái cùng lúc |
| Săn boss | Sát thương lên boss mỗi phút |
| Kỷ luật skill | Tỉ lệ skill dùng có tác dụng (trúng, chặn được đòn, thoát được vòng vây) |

Kèm theo: **tầm đánh ưa thích** và **nguyên nhân chết** hay gặp. Trình xem hiện profile này và so sánh
giữa các lần train.

## 5. Trình xem và giao diện

- **Trong trận:**
  - thanh EXP trên cùng, cấp, đồng hồ, số quái đã hạ, vàng nhặt được;
  - icon vũ khí và phụ kiện kèm cấp (game-icons.net);
  - thanh skill chủ động có vòng hồi chiêu;
  - thanh máu dưới chân nhân vật.
- **Bảng lên cấp:** 3–4 thẻ (icon, tên, cấp), **tô sáng thẻ AI chọn**, game tạm dừng khoảng 1 giây rồi
  chạy tiếp.
- **Lớp phủ khán giả (M5):** nhãn cho biết AI đang làm gì: **THẢ DIỀU / NHẶT VÀNG / LAO VÀO / THOÁT
  VÂY**. Nhãn phải **trung thực**: tính từ dữ liệu thật (hướng chạy so với đám quái, đồ nhặt gần đó, máu),
  không phải đoán ý định của mạng.
- **Màn kết trận:** thời gian sống, cách kết thúc và nguyên nhân chết, cấp đạt, số quái hạ, vàng nhận
  (cộng vào ví).
- **Báo cáo "câu chuyện" sau trận (M5):** vài dòng kể lại trận đấu từ telemetry, ví dụ "Phút 4 chọn Búa
  ném và bắt đầu thả diều; phút 9 suýt chết (còn 12% máu) vì bị Brute dồn góc; phút 15 gây 40% máu boss".
- **Menu (M5):**
  - Nhân vật: cấp, mua cấp, cộng/tẩy điểm chỉ số, 5 loadout;
  - Cửa hàng: mua class;
  - Chọn bậc;
  - Xem trận, Farm tự động;
  - TRAIN (kèm chọn Training Focus) / Power / TRAINING DATA (giữ như hiện nay);
  - **So sánh loadout**, Behavior Profile.
- **Lịch sử não (M6):** xem, so sánh, quay lại, nhân bản, đặt tên các phiên bản não, và tách nhánh (ví dụ
  một nhánh chuyên farm vàng, một nhánh chuyên săn boss).
- **Asset miễn phí (D-025):**
  - KayKit Halloween Bits (CC0) cho nghĩa địa;
  - bộ nhân vật và zombie KayKit đang có;
  - game-icons.net (CC BY 3.0) cho icon;
  - Kenney UI (CC0) cho khung giao diện.

## 6. Lộ trình

| # | Tên | Nội dung | Nghiệm thu |
|---|---|---|---|
| **M4A** | Lát cắt dọc Survivor | 1 bản đồ, 2 vũ khí (Kiếm quét, Búa ném), 4 phụ kiện, 3 skill, EXP và lên cấp, 3 loại quái, tinh anh, boss, quan sát v4, thưởng mới, bộ đánh giá, trình xem trận | Test xanh; bộ đánh giá 100 seed bậc 1: trung vị ≥ 10:00, P10 ≥ 7:00, không trận nào chết trước 3:00; owner xem được trận |
| **M4B** | Não thích nghi | Não theo build, `brain_upgrade`, champion/challenger, curriculum ôn tập, telemetry, Behavior Profile | Challenger chỉ thay champion khi thắng; nâng cấp não giữ nguyên hành vi; profile hiện được |
| **M4C** | Nội dung | Đủ 6 vũ khí, 8 phụ kiện, Spitter, nam châm, rương, đánh bóng HUD | Não học tiếp với nội dung mới, không học lại từ đầu; vẫn qua bộ đánh giá |
| **M5** | Kinh tế và build | Mục 3.1–3.5, 3.7: vàng về ví, cấp nhân vật, loadout, Training Focus, modifier độ khó, lớp phủ khán giả, báo cáo sau trận, telemetry kinh tế | Đổi build → train → AI đổi lối đánh (thấy ở Behavior Profile), điểm hồi phục nhanh |
| **M6** | Lịch sử não | Xem, so sánh, quay lại, nhân bản, đặt tên, tách nhánh các phiên bản não | Quay lại một não cũ và chơi được ngay |
| **M7** | Class mua được | Mage, Archer, tiến hóa vũ khí, thêm quái vào chỗ trống | Mua class bằng vàng; não nền mới qua bộ đánh giá |
| **M8** | Đánh bóng | Âm thanh, cân bằng, bản build hoàn chỉnh | File exe chạy trọn vòng chơi |

Năm hệ thống quan trọng nhất: **Champion/Challenger**, **Behavior Profile + telemetry**, **Training
Focus**, **Build Loadout + so sánh**, **Lịch sử não**.
