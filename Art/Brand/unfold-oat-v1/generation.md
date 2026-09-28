# 생성 기록

2026-09-28. 내장 image_gen으로 승인된 수정본을 오트 팔레트로 변환했습니다. 생성 PNG는 source/에 보존합니다. 배포용 파일은 동일 심벌을 정밀 SVG 도형으로 정리하고 승인본의 Unfold 글자 윤곽을 추출해 고정 팔레트로 내보낸 결과입니다. 별도 폰트 설치가 필요하지 않습니다.

배경 있는 image_gen 앱 아이콘 후보는 가장자리와 배경 얼룩이 있어 최종 파일에서 제외했습니다. 배포용 앱 아이콘은 동일 심벌의 SVG에서 렌더링했습니다.

## logo-oat-master

```text
Use case: precise-object-edit / logo-brand.
Input is the approved Unfold logo revision. Extract and recolor its design for a production logo asset, faithfully preserving the approved shape and character. Do NOT design a new logo.
The approved symbol has TWO vertical bars. LEFT: a tall rectangle with nearly square corners, only tiny corner rounding. RIGHT: same flat top edge and same tiny rounding of the two top corners, while its lower part retains the distinctive smoothly curved comma tail sweeping left. Preserve the precise proportions, gap and lower comma curve shown in the input. Never change the bars back into capsules.
Use perfectly FLAT SOLID colors, crisp clean anti-aliased edges, NO gradients, texture, shadows, glows, borders or shading. Genuine alpha transparency outside the artwork, NOT a painted checkerboard, cream page or white rectangle.
Remove the concept heading and presentation-sheet labels and remove all repeated usage studies. This output is the usable isolated asset itself.
Asset: the single main horizontal brand lockup only, with the approved two-bar symbol at the left and exact word "Unfold" at the right. Faithfully preserve the existing wordmark letterforms and their weight, spacing and relationship to the symbol, including the softly rounded lettering. Text must read exactly "Unfold". Do NOT add any other words or labels.
Recolor ONLY: left symbol bar solid terracotta #985139; right comma bar solid oat-apricot #E9B894; wordmark solid warm dark brown #342D28.
Output a high-resolution wide transparent PNG, ideally 3072 x 1024. Center the lockup in the canvas with modest consistent transparent outer margin; make the design as large as possible without clipping. Transparent empty background everywhere outside the symbol and lettering.
```

## symbol-oat-master

```text
Use case: precise-object-edit / logo-brand.
Input is the approved Unfold logo revision. Extract and recolor its design for a production logo asset, faithfully preserving the approved shape and character. Do NOT design a new logo.
The approved symbol has TWO vertical bars. LEFT: a tall rectangle with nearly square corners, only tiny corner rounding. RIGHT: same flat top edge and same tiny rounding of the two top corners, while its lower part retains the distinctive smoothly curved comma tail sweeping left. Preserve the precise proportions, gap and lower comma curve shown in the input. Never change the bars back into capsules.
Use perfectly FLAT SOLID colors, crisp clean anti-aliased edges, NO gradients, texture, shadows, glows, borders or shading. Genuine alpha transparency outside the artwork, NOT a painted checkerboard, cream page or white rectangle.
Remove the concept heading and presentation-sheet labels and remove all repeated usage studies. This output is the usable isolated asset itself.
Asset: ONLY the standalone two-bar pause/comma brand symbol from the input, with no text and no containing square or circle.
Recolor ONLY: left bar solid terracotta #985139; right comma bar solid oat-apricot #E9B894.
Output a high-resolution square transparent PNG, ideally 2048 x 2048. Center the symbol, preserving its approved width to height ratio. The COMPLETE symbol including the comma tail fits into the center 78% of the canvas height with balanced transparent margins. No text, no background, no container, no additional decoration.
```

## app-icon-oat-master

```text
Use case: precise-object-edit / logo-brand.
Input is the approved Unfold logo revision. Extract and recolor its design for a production logo asset, faithfully preserving the approved shape and character. Do NOT design a new logo.
The approved symbol has TWO vertical bars. LEFT: a tall rectangle with nearly square corners, only tiny corner rounding. RIGHT: same flat top edge and same tiny rounding of the two top corners, while its lower part retains the distinctive smoothly curved comma tail sweeping left. Preserve the precise proportions, gap and lower comma curve shown in the input. Never change the bars back into capsules.
Use perfectly FLAT SOLID colors, crisp clean anti-aliased edges, NO gradients, texture, shadows, glows, borders or shading. Genuine alpha transparency outside the artwork, NOT a painted checkerboard, cream page or white rectangle.
Remove the concept heading and presentation-sheet labels and remove all repeated usage studies. This output is the usable isolated asset itself.
Asset: ONLY the small APP ICON example from the approved presentation sheet, enlarged to a high-resolution square production app icon PNG, ideally 2048 x 2048. No text.
Preserve the original rounded-square container and the exact approved bar shapes inside it. Recolor the rounded-square container solid terracotta #985139 and BOTH inner bars solid light oat cream #FAF6EF, matching the original reversed one-color icon treatment.
The rounded square occupies exactly the entire square canvas extent, with its four outside corners genuinely transparent and a corner radius about 22% of the canvas width. No exterior whitespace except the corner cutouts. Keep the inner symbol centered at about 60% of the canvas height, generous padding, and the right comma tail entirely inside the container. There is NO border stroke, NO shadow, NO gloss, NO gradient. Solid terracotta inside the container all the way to the curve, transparent outside.
```

