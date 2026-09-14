# 보리 원본 생성 기록

- 생성일: 2026-09-14
- 도구: Codex 내장 `image_gen` (CLI/API fallback 사용 안 함)
- 입력 이미지: 없음. 기존 캐릭터·사진·특정 작가 작품을 참조하지 않음.
- 원본: `source/bori-atlas-v1.png`, 생성 결과 PNG 바이트를 그대로 보존.
- 편집: 원본 픽셀 수정·배경 제거·색상 변경 없음. 실행 manifest의 셀/프레임 순서와 시간을 별도로 지정.
- 원본 제작 방식과 입력 부재 기록이며, 상업적 권리 검토 완료나 독점 저작권의 증거는 아님.

## 최종 생성 프롬프트

```text
Use case: stylized-concept.
Asset type: production sprite atlas for an original desktop companion named Bori, a small calm cream-colored rabbit. Create ONE RGBA PNG sprite sheet on a genuinely transparent background, portrait 1024 by 1536 pixels. This is animation source artwork, not a mockup or poster.
Exactly 4 columns and 6 rows of equal 256 by 256 cells, 24 distinct ordered frames. No visible grid, cell borders, text, numbers, shadows cast onto a background, watermark or decorative objects. Keep every drawing entirely inside its cell with at least 24 pixels clear transparent margin. Center each rabbit on local x=128, grounded at local y=224, fixed camera, fixed scale and stable feet position. Rabbit sits upright facing slightly toward viewer right, full body visible, two long ears, two small front paws, two grounded back feet and one small tail. Same anatomy, face, color and proportions in ALL cells. Round cream body, warm caramel ear tips, tiny sage green neckerchief, dark warm-brown eyes and clean dark-brown contour. Soft polished 2D cel illustration with minimal shading, readable at 192px. Gentle, sleepy personality, no exaggerated excitement.
Read left to right then top to bottom:
Row 1, idle frames 0-3: neutral relaxed sitting eyes open; slightly half-closed eyes; eyes gently fully closed; eyes open again. Body, ears and paws otherwise remain nearly identical, for a quiet blink loop.
Row 2, attention frames 4-7: same neutral pose; one ear subtly perks; slight friendly head tilt; return almost to neutral. Both feet stay grounded. Do not wave or stretch.
Rows 3 and 4, stretch frames 8-15: neutral pose; both front paws begin lifting; paws at cheek level; both paws above head; hold a gentle full upward stretch, eyes closed; ease paws down to cheek level; paws lower; return exactly toward the neutral sitting pose. Ears remain TWO recognizable rabbit ears behind the TWO paws. No extra limbs.
Row 5, celebrate frames 16-19: neutral; both front paws gently meet at the chest; delighted small closed-eye smile with paws together; return neutral. No confetti, icons, hearts or jumping.
Row 6, click frames 20-23: neutral; one ear folds forward slightly; a small inquisitive head tilt with the ear folding; return neutral. This is distinct from the upward stretch and chest-paw celebration.
All frames use the SAME character design and baseline. No cropping, bleed across cells or irregular spacing. Actual transparent alpha outside each rabbit, not a painted checkerboard or solid background.
```
