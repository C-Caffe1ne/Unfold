# 강아지·고슴도치·펭귄 생성 기록

2026-09-22 · 내장 image_gen · 기준: docs/pet-generation-guide.md.
참조: original-companions-v2/source/mochi-atlas.png, bori-atlas.png.
두 이미지는 스타일 참조이며 기존 파일을 교체하지 않는다.

## 공통 격자 보정 프롬프트

Edit this sprite atlas. Preserve the exact animal design, face, colors, thick brown line art and all 24 pose identities. ONLY FIX THE TECHNICAL 4x6 GRID LAYOUT. Current atlas compresses the lower rows and sprites cross the 256-pixel cell boundaries. Reconstruct a perfectly even 4-column, 6-row atlas, total 1024x1536 transparent RGBA PNG, each cell 256x256.
Put each COMPLETE animal at the center of its own cell. Cell center coordinates MUST be:
row1 y=128: x=128,384,640,896;
row2 y=384: x=128,384,640,896;
row3 y=640: x=128,384,640,896;
row4 y=896: x=128,384,640,896;
row5 y=1152: x=128,384,640,896;
row6 y=1408: x=128,384,640,896.
The whole animal INCLUDING ears, back spines, flippers, tail and feet must be no more than 168px high AND no more than 190px wide. Each animal silhouette must be entirely contained within x=30..226 and y=40..216 relative to its own cell. This requires plentiful EMPTY TRANSPARENT SPACE between rows, particularly between rows4,5,6. Do NOT crop limbs or shrink only the ears. Maintain identical head/body scale across cells. Sleeping and stretching animals can be naturally lower. All right-facing walk and stretch poses stay facing right. Do not retain the current compressed vertical spacing. No cell may contain a fragment from another animal.
Preserve the existing row-major pose sequence exactly: row1 neutral/blink/look-left/look-right; row2 drowsy/sleep/breath/yawn; row3 attention/stretch-start/stretch-full/recovery; row4 happy/startled/startled-left/startled-right; row5 sulk/grumpy/walk1/walk2; row6 walk3/walk4/content/neutral.
No text, numbers, visible grid, boxes, ground, shadows or backdrop. Absolutely transparent alpha=0 outside the animal, including all gaps and outer edges. The image is a machine-sliced game atlas, not a poster.

## 강아지 (puppy-dog)

Use case: stylized-concept. Create a production sprite atlas for the Unfold desktop pet app, as a new animal variant of the supplied reference atlases. Reference images are ONLY style and pose-language references: do not change or output the cat or rabbit. Match their simple flat hand-drawn appearance: a clean thick rounded warm dark-brown outline, flat pale fills, tiny dark oval eyes, minimal details, consistent anatomy across every frame. No pixel art, no 3D, no painterly texture, no oversized mascot head.
CRITICAL OUTPUT: one truly transparent RGBA PNG, exactly 1024x1536 pixels. EXACT uniform 4 columns by 6 rows, 24 separate 256x256 cells, row-major indexing 0..23. No visible grid, no labels, text, backdrop, floor ellipse, shadow, glow, particles, accessories or decorative marks outside the animal. Ignore any backdrop visible behind the reference; every pixel outside the animal must have alpha=0.
GRID SAFETY: each entire full-body sprite must fit inside its own cell with empty transparent separation. Horizontal centers are x=128,384,640,896. Foot baselines per row are y=220,476,732,988,1244,1500. Within EVERY cell: complete silhouette confined to local x=28..228 and y=40..222. Maximum whole animal height 175px, including ears/spines, and maximum width 190px. Keep scale and feet baseline consistent. Lower sleeping/stretched poses retain a natural low silhouette. Never let the next row's ears/spines enter another cell.
24 poses in this EXACT order:
Row 1: 0 neutral sitting/upright resting eyes open; 1 identical rest eyes closed blink; 2 resting looking left; 3 resting looking right.
Row 2: 4 drowsy half-closed eyes; 5 curled/tucked asleep; 6 identical asleep with subtle breathing; 7 resting yawning.
Row 3: 8 alert surprised attention; 9 starting its species-appropriate gentle stretch facing RIGHT; 10 full stretch facing RIGHT; 11 ending stretch returning to rest.
Row 4: 12 happy resting eyes smiling; 13 startled wide eyes and tiny open mouth/beak; 14 startled looking left; 15 startled looking right.
Row 5: 16 sulking head turned aside; 17 mildly grumpy lowered brows, still gentle; 18 side profile walking RIGHT first step; 19 same walking RIGHT next step.
Row 6: 20 walking RIGHT opposite step; 21 walking RIGHT next step; 22 content recovery resting; 23 exactly the neutral resting design of cell 0.
All four walk sprites face RIGHT, same proportions and body size, only natural small limb movement. Do NOT draw multiple overlaid poses or speed trails in one cell. Squash/bounce and desktop translation are handled by the app, not baked into this atlas.
Character: A small pale honey-cream DOG, with two floppy rounded light caramel ears, short rounded muzzle, tiny dark-brown oval nose, small round paws, and a short gently curved tail. Clear dog anatomy, not cat or rabbit. No collar, tags, spots, clothes, or props. Simple off-white/cream body and muted caramel ears, same warm brown outline as references. Four-legged stretches lower the front paws while the rear is raised; both stretch poses face right.

## 고슴도치 (hedgehog)

Use case: stylized-concept. Create a production sprite atlas for the Unfold desktop pet app, as a new animal variant of the supplied reference atlases. Reference images are ONLY style and pose-language references: do not change or output the cat or rabbit. Match their simple flat hand-drawn appearance: a clean thick rounded warm dark-brown outline, flat pale fills, tiny dark oval eyes, minimal details, consistent anatomy across every frame. No pixel art, no 3D, no painterly texture, no oversized mascot head.
CRITICAL OUTPUT: one truly transparent RGBA PNG, exactly 1024x1536 pixels. EXACT uniform 4 columns by 6 rows, 24 separate 256x256 cells, row-major indexing 0..23. No visible grid, no labels, text, backdrop, floor ellipse, shadow, glow, particles, accessories or decorative marks outside the animal. Ignore any backdrop visible behind the reference; every pixel outside the animal must have alpha=0.
GRID SAFETY: each entire full-body sprite must fit inside its own cell with empty transparent separation. Horizontal centers are x=128,384,640,896. Foot baselines per row are y=220,476,732,988,1244,1500. Within EVERY cell: complete silhouette confined to local x=28..228 and y=40..222. Maximum whole animal height 175px, including ears/spines, and maximum width 190px. Keep scale and feet baseline consistent. Lower sleeping/stretched poses retain a natural low silhouette. Never let the next row's ears/spines enter another cell.
24 poses in this EXACT order:
Row 1: 0 neutral sitting/upright resting eyes open; 1 identical rest eyes closed blink; 2 resting looking left; 3 resting looking right.
Row 2: 4 drowsy half-closed eyes; 5 curled/tucked asleep; 6 identical asleep with subtle breathing; 7 resting yawning.
Row 3: 8 alert surprised attention; 9 starting its species-appropriate gentle stretch facing RIGHT; 10 full stretch facing RIGHT; 11 ending stretch returning to rest.
Row 4: 12 happy resting eyes smiling; 13 startled wide eyes and tiny open mouth/beak; 14 startled looking left; 15 startled looking right.
Row 5: 16 sulking head turned aside; 17 mildly grumpy lowered brows, still gentle; 18 side profile walking RIGHT first step; 19 same walking RIGHT next step.
Row 6: 20 walking RIGHT opposite step; 21 walking RIGHT next step; 22 content recovery resting; 23 exactly the neutral resting design of cell 0.
All four walk sprites face RIGHT, same proportions and body size, only natural small limb movement. Do NOT draw multiple overlaid poses or speed trails in one cell. Squash/bounce and desktop translation are handled by the app, not baked into this atlas.
Character: A small HEDGEHOG with a pale cream face and belly, a rounded warm taupe-brown back made of a few broad soft triangular spines, small round ears, tiny dark nose, very short feet. Use only a simple outer zigzag silhouette for the spines, no dense hair lines or texture. Compact natural hedgehog proportions with a gently pointed snout. Neutral rest sits slightly upright showing its cream belly. Sleep curls into a compact rounded shape with the face still recognizable. Stretch is a low gentle forward extension facing right. Walk has low rounded body and tiny alternating feet, all four walk cells face right. No props or accessories.

## 펭귄 (penguin)

Use case: stylized-concept. Create a production sprite atlas for the Unfold desktop pet app, as a new animal variant of the supplied reference atlases. Reference images are ONLY style and pose-language references: do not change or output the cat or rabbit. Match their simple flat hand-drawn appearance: a clean thick rounded warm dark-brown outline, flat pale fills, tiny dark oval eyes, minimal details, consistent anatomy across every frame. No pixel art, no 3D, no painterly texture, no oversized mascot head.
CRITICAL OUTPUT: one truly transparent RGBA PNG, exactly 1024x1536 pixels. EXACT uniform 4 columns by 6 rows, 24 separate 256x256 cells, row-major indexing 0..23. No visible grid, no labels, text, backdrop, floor ellipse, shadow, glow, particles, accessories or decorative marks outside the animal. Ignore any backdrop visible behind the reference; every pixel outside the animal must have alpha=0.
GRID SAFETY: each entire full-body sprite must fit inside its own cell with empty transparent separation. Horizontal centers are x=128,384,640,896. Foot baselines per row are y=220,476,732,988,1244,1500. Within EVERY cell: complete silhouette confined to local x=28..228 and y=40..222. Maximum whole animal height 175px, including ears/spines, and maximum width 190px. Keep scale and feet baseline consistent. Lower sleeping/stretched poses retain a natural low silhouette. Never let the next row's ears/spines enter another cell.
24 poses in this EXACT order:
Row 1: 0 neutral sitting/upright resting eyes open; 1 identical rest eyes closed blink; 2 resting looking left; 3 resting looking right.
Row 2: 4 drowsy half-closed eyes; 5 curled/tucked asleep; 6 identical asleep with subtle breathing; 7 resting yawning.
Row 3: 8 alert surprised attention; 9 starting its species-appropriate gentle stretch facing RIGHT; 10 full stretch facing RIGHT; 11 ending stretch returning to rest.
Row 4: 12 happy resting eyes smiling; 13 startled wide eyes and tiny open mouth/beak; 14 startled looking left; 15 startled looking right.
Row 5: 16 sulking head turned aside; 17 mildly grumpy lowered brows, still gentle; 18 side profile walking RIGHT first step; 19 same walking RIGHT next step.
Row 6: 20 walking RIGHT opposite step; 21 walking RIGHT next step; 22 content recovery resting; 23 exactly the neutral resting design of cell 0.
All four walk sprites face RIGHT, same proportions and body size, only natural small limb movement. Do NOT draw multiple overlaid poses or speed trails in one cell. Squash/bounce and desktop translation are handled by the app, not baked into this atlas.
Character: A small PENGUIN with a soft warm charcoal-gray head/back and flippers, pale creamy white face patch and belly, tiny muted apricot beak and short apricot webbed feet. Rounded upright penguin silhouette, modest head-to-body proportions, two small oval eyes. Keep the warm dark-brown outline and flat simple fills of the references. Sleep tucks its beak down toward the chest; stretching raises and extends the flippers with a gentle rightward lean, not a four-legged cat pose. Walking is a right-facing penguin waddle with alternating short feet and natural small flipper movement. No scarf, hat, clothes, ice, snow, props or accessories.

## 최종 시트 재구성

4×6 후보와 격자 보정 후보에서 셀 침범이 남아, 같은 캐릭터를 참조하여 4×4 시트로 재생성한다. 행동 10종은 유지하고 16개 포즈를 타임라인으로 조합한다.

Create a NEW SQUARE 1024x1024 transparent sprite sheet. Use the attached animal ONLY as a character/style reference, NOT a layout reference. Keep exactly that species, face, colors, warm brown thick outline, simple flat drawing.
A perfectly uniform FOUR BY FOUR arrangement of SIXTEEN FULL BODY animals. Each animal is SMALL, occupying at most 150x160 pixels, centered in a 256x256 cell. Plenty of empty space above and below every animal. Equal spacing throughout the entire square. Exact column centers 128,384,640,896. Exact row centers 128,384,640,896. Do not create a tall sheet. Do not draw 24 animals. SIXTEEN only.
Row1: neutral rest; same rest blinking eyes closed; rest looking left; rest looking right.
Row2: drowsy rest; sleeping compact; yawning; alert watching.
Row3: species appropriate full stretch leaning/facing RIGHT; happy smiling rest; startled eyes open with small round mouth; mildly sulking/grumpy rest.
Row4: four different alternating walking/waddling footstep poses ALL facing RIGHT.
Consistent body size and face. Each whole body including all tail/ear/spines/flippers fits well within its individual 256-square with at least 40px of completely transparent gap on all four sides. Keep all sixteen separate, no fragments, no overlapping, no motion trails. Transparent RGBA, alpha zero outside the animals. No shadows, ground, texture, backdrop, text, grid lines, props. Do not reuse the reference's rectangular grid. Make a fresh SQUARE 4x4 atlas with smaller, widely separated characters.

## 채택 결과와 후처리

실제 출력은 세 파일 모두 1254×1254 RGBA다. 1024×1024 출력 지시와 다르다.
고슴도치의 스트레칭 자세는 단순 4등분 경계를 넘지만 원본 그림은 온전히 존재한다.
사용자에게 원본 보존 후 코드 정렬 방식을 제시했고, 후속 “작업 진행해줘” 요청에 따라
`build-assets.py`로 자세를 잘라 256×256 셀에 배치했다. 그림 픽셀을 재도색하거나
리샘플링하지 않았다. 최종 실행 규격과 원본 좌표는 README와 제작 원장을 따른다.

| 동물 | 채택한 image_gen 출력 파일 | 보존된 최종 후보 |
|---|---|---|
| 강아지 | `exec-e643b672-d8a9-4692-b6df-8a577e00e4ed.png` | `source/candidates/puppy-dog-square.png` |
| 고슴도치 | `exec-3de01bec-3c92-437a-9145-9dae9f5ab2a9.png` | `source/candidates/hedgehog-square.png` |
| 펭귄 | `exec-4ac5194b-18b5-45e8-b332-e7ea266c3db0.png` | `source/candidates/penguin-square.png` |
