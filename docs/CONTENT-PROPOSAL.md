# Đề xuất mở rộng vũ khí, phụ kiện và skill (tham khảo Vampire Survivors)

> Soạn 2026-10-02 theo yêu cầu của owner: "thêm rất nhiều vũ khí và skill để lối đánh linh hoạt hơn".
> Nguồn ý tưởng: bảng vũ khí và phụ kiện của vampire.survivors.wiki (chỉ lấy **ý tưởng cơ chế**).
> Tên, mô tả, hình ảnh và âm thanh của ta tự đặt (AGENTS.md §5: asset phải CC0/MIT hoặc CC BY có ghi công).
> Lưu ý: bảng wiki đọc qua công cụ tóm tắt, mô tả từng vũ khí chỉ một dòng; trước khi làm từng món sẽ đọc lại trang riêng của nó.

## 1. Hiện có gì, còn chỗ nào

| Thứ | Hiện có | Giới hạn thật trong code |
|---|---|---|
| Vũ khí | 18 (Warrior 0–5, Mage 14–19, Archer 20–25) + 18 bản tiến hóa (40–57) | Danh mục 64 ô. **Còn trống 18 ô: 26–39 và 58–61** |
| Kiểu đánh (`WeaponPattern`) | Sweep, Thrust, Orbit, Thrown, Aura, Shockwave, Strike, Fan | Mỗi class chỉ giữ 1 Orbit, 1 Aura, 1 Shockwave (sim chỉ có 1 trạng thái cho mỗi kiểu) |
| Phụ kiện | 8 (cũ) | Chỉ số Hồi phục, May mắn, Tham lam, Tăng trưởng, Chí mạng kép **đã có trong sim** (từ điểm chỉ số của owner) nhưng **chưa có phụ kiện nào dùng**. Còn 3 chỗ chỉ số trống (`Reserved13–15`) |
| Ô mang theo | 4 vũ khí + 4 phụ kiện | **Không thuộc schema** (túi đồ quan sát theo ô danh mục), nâng lên 6 + 6 được mà không đổi não |
| Skill chủ động | Warrior: Đá, Khiên, Lướt, **(trống)**. Archer: Bắn xuyên, Lộn lùi, Đá, **(trống)**. Mage: đủ 4 | Hành động nhánh 2 = 5 (không + 4 skill). **Warrior và Archer còn 1 ô skill trống, thêm không đổi schema**. Mage cần nới 4 → 6 (đổi schema) |

Kết luận: **đợt 1 không phải đổi schema** (não đang train học tiếp) nhưng chỉ chứa được tối đa khoảng 14 món mới. "Rất nhiều" (50+) cần **đợt 2: nới danh mục 64 → 128** bằng `brain_upgrade` (D-027, hạ tầng đã có từ T-019): não cũ được nâng, không học lại từ đầu. Quan sát tăng khoảng 2264 → 2584 (ước lượng: túi đồ +64, 4 bảng đề nghị × 64).

Tiến hóa dùng cố định 40–57 (3 class × 6). Vũ khí mới ở đợt 1 **chưa có bản tiến hóa**; thêm khi sang đợt 2.

## 2. Vũ khí đề xuất (theo thứ tự nên làm)

"Cần code" = chỉ chỉnh số trong catalog (**C**), hoặc thêm một ít logic (**S**), hoặc thêm kiểu đánh mới (**M**). Cột cuối là điều nó buộc AI học, vì chính điều này làm lối đánh đa dạng (GDD §2 "mỗi món phải buộc AI chơi khác đi").

### Nhóm A: gần như chỉ chỉnh số (C), làm trước

| Tên đề xuất | Ý tưởng wiki | Cách làm trong game của ta | Class | AI phải học |
|---|---|---|---|---|
| Hỏa cầu nổ | Fire Wand | Strike (đã có) với sát thương lớn, vùng nổ rộng, hồi chiêu dài | Mage | Chọn khi đám đông dày thay vì ít quái |
| Phun lửa | Flames of Misspell | Sweep hình nón hẹp, hồi chiêu ngắn, đốt liên tục | Warrior, Mage | Đứng sát quái, quay đúng hướng |
| Vòng tay ba mũi | Bracelet | Fan 3 viên bắn vào 1 mục tiêu | Archer, Mage | Dồn sát thương lên một con (tinh anh, trùm) |
| Kiếm liên hoàn | Victory Sword | Sweep đánh combo 3 nhát, nhát cuối mạnh | Warrior | Đứng yên đủ lâu để hoàn thành combo |
| Búa nặng | Axe | Thrown ném cao, sát thương lớn, vùng lớn, chậm | Warrior | Dự đoán vị trí quái |

### Nhóm B: thêm một ít logic (S)

| Tên đề xuất | Ý tưởng wiki | Cách làm | Class | AI phải học |
|---|---|---|---|---|
| Bom | Cherry Bomb | Thrown + nổ vùng khi chạm hoặc hết tầm | Warrior, Archer | Ném vào cụm quái đông để nổ trúng nhiều con |
| Bắn bốn hướng | Phiera Der Tuphello | Fan cố định 4 hướng theo thân người | Archer | Xoay người, đứng giữa đám quái |
| Phản đòn | Pako Battiliar | Khi mình trúng đòn, đánh trả quanh người | Warrior | Chịu đòn có chủ ý, ghép với giáp |
| Vòng bảo hộ | Laurel | Chắn hấp thụ N đòn rồi hồi lại sau vài giây | Warrior, Mage | Lùi lại chờ chắn hồi, đổi nhịp đánh/né |
| Đá tụ lực | Magi-Stone | Sát thương cố định theo cấp, không phụ thuộc Găng | Mage | Cân giữa món "ổn định" và build sát thương |

### Nhóm C: kiểu đánh mới (M), làm sau khi A và B ổn

| Tên đề xuất | Ý tưởng wiki | Cách làm | Class | AI phải học |
|---|---|---|---|---|
| Boomerang | Cross | Bay ra quái gần nhất rồi quay về, đánh hai lần | Warrior, Archer | Đứng thẳng hàng với bầy quái |
| Đạn nảy | Runetracer, Bone | Nảy khỏi tường, vật cản và quái | Archer, Mage | Đứng gần tường/vật cản để đạn nảy nhiều |
| Bình độc | Santa Water | Ném xuống vùng sát thương 3–4 s, vùng chặn đường | Mage, Archer | **Lùa quái vào vùng rồi chạy vòng**, kiểu "kite" |
| Bóng tốc | Shadow Pinion, Vento Sacro | Mạnh hơn khi di chuyển liên tục, đứng yên thì yếu | Archer | Không dừng lại; đối lập với Kiếm liên hoàn |
| Đồng hồ băng | Clock Lancet | Xác suất đóng băng mọi quái trong màn hình 1–2 s | Mage | Dùng khoảng đóng băng để thoát vây hoặc dồn sát thương |
| Thanh tẩy | Pentagram | Hồi chiêu rất dài, xoá quái thường trong tầm nhìn (không tính trùm) | Mage | Giữ đòn đắt cho lúc bị vây; nhưng **dễ phá cân bằng**, cần thử kỹ |
| Mưa bom vòng | Peachone, Ebony Wings | Rải sát thương vào các điểm quanh người theo vòng | Mage | Vị trí quanh người quan trọng hơn hướng mặt |

## 3. Phụ kiện đề xuất

| Tên đề xuất | Ý tưởng wiki | Hiệu ứng mỗi cấp | Cần code |
|---|---|---|---|
| Hồi phục | Pummarola | +0,2 máu/giây | Nối chỉ số Regen vào phụ kiện (C) |
| Cỏ may mắn | Clover | +May mắn (tăng tỉ lệ rớt, chí mạng đồ) | C |
| Tham lam | Stone Mask | +vàng nhặt được | C |
| Vương miện | Crown | +EXP nhận được | C |
| Bùa thời gian | Spellbinder | +thời lượng vũ khí (Orbit, Aura, vùng) | Chỉ số mới (S) |
| Bộ nhân đôi | Duplicator | +1 viên/đòn cho mọi vũ khí có số lượng (tối đa cấp 2) | Chỉ số mới (S) |
| Quả tim hồi sinh | Tirajisú | +1 lần hồi sinh nửa máu (tối đa cấp 2) | Cơ chế mới (M) |
| Hộp tổng hợp | Torrona's Box | cộng nhỏ vào nhiều chỉ số | C |
| Giáp phản | Armor | giáp + phản sát thương | S |

Phụ kiện còn là **vế thứ hai của tiến hóa**: mỗi phụ kiện mới là một cặp mới cho tiến hóa ở đợt 2.

## 4. Skill chủ động đề xuất

Skill nằm ở hành động nhánh 2 (không + 4 skill).

| Skill | Class | Mô tả | Cần schema? |
|---|---|---|---|
| Tiếng thét chiến trận | Warrior (ô trống) | Đẩy lùi và choáng quanh người, tăng sát thương ngắn | Không |
| Nhảy đập đất | Warrior | Bật tới vị trí chỉ định rồi giáng xuống, sát thương vùng | Không |
| Xoáy kiếm | Warrior | Xoay 360° sát thương liên tục 1–2 s | Không |
| Bẫy gai | Archer (ô trống) | Thả vùng bẫy tại chỗ làm chậm và gây sát thương | Không |
| Loạt tên (`arrow-barrage`; đổi tên 2026-10-03 cho khỏi trùng vũ khí Mưa tên) | Archer | Rải loạt tên theo hình nón phía trước | Không |
| Khói ẩn thân | Archer | Quái mất mục tiêu trong 2 s | Không |
| Mage thêm 2 skill | Mage | Ví dụ Tường lửa, Triệu hồi bóng | **Có** (nới 4 → 6 skill, đợt 2) |

Mỗi class chỉ chọn 1 trong các skill đề xuất cho ô trống ở đợt 1; ô thứ 5, 6 chờ đợt 2.

## 5. Kế hoạch đề xuất

1. **Đợt 1a (không đổi schema)**: nhóm A (5 vũ khí) + 4 phụ kiện nhóm C (Hồi phục, May mắn, Tham lam, Vương miện) + 1 skill mới cho Warrior và 1 cho Archer. Khoảng 9 ô trong 18 ô trống.
2. **Đợt 1b (không đổi schema)**: nhóm B (5 vũ khí) và Bùa thời gian, Bộ nhân đôi. Nâng ô mang theo 4 + 4 lên 6 + 6 sau khi đo cân bằng.
3. **Đợt 2 (đổi schema bằng `brain_upgrade`)**: nới danh mục 64 → 128, thêm skill 4 → 6, nhóm C, tiến hóa cho vũ khí mới.
4. Mỗi đợt kèm: test (đối chiếu mô phỏng tất định, không cấp phát bộ nhớ), cân bằng bằng `SurvivorEval`, hình ảnh/icon (game-icons.net CC BY, KayKit CC0) ở phía Unity, rồi train tiếp.

### Rủi ro cần biết

- **Não chưa thấy món mới.** Các ô 26–39 và 58–61 hiện luôn bằng 0 trong quan sát. Trọng số cho các ô này chưa từng được huấn luyện, nên lần đầu món mới xuất hiện, AI có thể chọn kém đi một thời gian. Cách giảm: đặt các trọng số này về 0 trước khi bật món mới (chưa kiểm chứng trên code, cần xem lại khi làm).
- **Nhiều món không đồng nghĩa AI giỏi hơn.** Mỗi món mới làm bài toán chọn khó hơn; cần theo dõi đánh giá 100 seed (trung vị ≥ 10:00, P10 ≥ 7:00) sau mỗi đợt.
- **Phá cân bằng.** Thanh tẩy, Bộ nhân đôi và Hồi sinh dễ vượt trội; làm sau, thử riêng.
- **Phần hiển thị chưa tính ở đây.** Mỗi vũ khí mới cần hình ảnh/hiệu ứng/icon trong trình xem (việc này chỉ kiểm được trên PC có Unity).

## 6. Cần owner quyết định

1. Có đồng ý chia hai đợt (đợt 1 không đổi schema, đợt 2 nới danh mục)?
2. Có nâng ô mang theo lên 6 vũ khí + 6 phụ kiện giống bản gốc không (làm con số nhiều hơn, build đa dạng hơn, nhưng AI khó chọn hơn)?
3. Muốn ưu tiên class nào nhận món mới trước?
