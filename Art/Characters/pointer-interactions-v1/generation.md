# Pointer interaction art — 2026-09-24

Built-in `image_gen` mode. References are existing original pets. Original source images remain unchanged. New generated candidates are stored under `source/` before sprite assembly.

## hedgehog

Reference: `Assets/Characters/hedgehog/spritesheet.png`

```text
Use case: stylized-concept. Production animation sprite sheet for an existing desktop companion. The provided sheet is a CHARACTER IDENTITY AND STYLE REFERENCE only. Create NEW poses of exactly this same animal. Preserve its exact palette, head shape, eye and nose style, outline weight and simplicity. Soft hand-drawn 2D, rounded dark warm-brown outlines, flat soft fills. No 3D, no new markings, no clothing.
Output a 1024x512 transparent RGBA PNG arranged as EXACTLY FOUR columns and TWO rows, 8 isolated sprites, each in its own 256x256 cell, no visible grid. All cells face the viewer or the same slight 3/4 angle, constant head size and body proportions. Center each animal at x=128 within its cell; entire silhouette strictly inside x=24..232,y=24..228. Paws/lowest edge at y=224. No overlap across cells. Real alpha transparency; no painted checkerboard, no background color, no floor, no oval/shadow/glow, no text or labels, no props, no hand, no cursor. Motion/height will be animated in the application; do not put motion trails or duplicate silhouettes in a cell.
Character: the exact brown-spined, cream-faced little hedgehog in the reference.
Row 1 left to right: (0) begins curling, head tucks down, paws draw inward; (1) nearly curled, face partly hidden by curled brown spiny body; (2) FULLY CURLED INTO A ROUND BALL, a single compact circular brown spiny silhouette with subtle curled spine lines, no face, ears or feet showing; (3) same fully closed round spiny ball, tiny breathing variation, same outline and size.
Row 2 left to right: (4) same fully closed round ball at rest; (5) begins opening after landing, cream face and tiny nose peek out of a small opening in the ball; (6) half uncurled, face and front paws emerge, spiny back still rounded; (7) fully uncurled in original neutral sitting pose, matching first neutral frame of reference.
The round ball should have diameter about 160px and the same visual mass as the original animal. Make curling and opening gradual and coherent. The held state MUST be a true BALL, not a sleeping hedgehog with its face visible.
```

## default-cat

Reference: `Art/Characters/original-companions-v2/source/mochi-atlas.png`

```text
Use case: stylized-concept. Production animation sprite sheet for an existing desktop companion. The provided sheet is a CHARACTER IDENTITY AND STYLE REFERENCE only. Create NEW poses of exactly this same animal. Preserve its exact palette, head shape, eye and nose style, outline weight and simplicity. Soft hand-drawn 2D, rounded dark warm-brown outlines, flat soft fills. No 3D, no new markings, no clothing.
Output a 1024x512 transparent RGBA PNG arranged as EXACTLY FOUR columns and TWO rows, 8 isolated sprites, each in its own 256x256 cell, no visible grid. All cells face the viewer or the same slight 3/4 angle, constant head size and body proportions. Center each animal at x=128 within its cell; entire silhouette strictly inside x=24..232,y=24..228. Paws/lowest edge at y=224. No overlap across cells. Real alpha transparency; no painted checkerboard, no background color, no floor, no oval/shadow/glow, no text or labels, no props, no hand, no cursor. Motion/height will be animated in the application; do not put motion trails or duplicate silhouettes in a cell.
Character: exact Mochi cream cat, triangle ears with peach pink inside, tiny oval dark eyes, tiny mouth, three short whiskers, curved long tail.
Create a gentle cartoon SCRUFF-LIFT pose, as if an invisible pointer pinches loose fur at the back of the neck. Clearly draw a small lifted scruff fold just behind the head; the body hangs BELOW the neck, front paws and hind paws both dangling down with visible air between feet. This is relaxed and cute, no distress or injury. Never draw a hand or an external grabbing object.
Row 1: (0) beginning to be picked up, shoulders raised, paws loosening; (1) body lifting, hind legs starting to dangle; (2) fully suspended by SCRUFF, head slightly lowered, all four short legs dangling, tail hangs curved downward; (3) same suspended pose with a blink, no silhouette jump.
Row 2: (4) first landing crouch, paws meet floor, body low; (5) deeper soft landing crouch; (6) halfway returning to sitting; (7) original neutral sitting pose from reference. Match scale and expression style throughout.
```

## bori-rabbit

Reference: `Art/Characters/original-companions-v2/source/bori-atlas.png`

```text
Use case: stylized-concept. Production animation sprite sheet for an existing desktop companion. The provided sheet is a CHARACTER IDENTITY AND STYLE REFERENCE only. Create NEW poses of exactly this same animal. Preserve its exact palette, head shape, eye and nose style, outline weight and simplicity. Soft hand-drawn 2D, rounded dark warm-brown outlines, flat soft fills. No 3D, no new markings, no clothing.
Output a 1024x512 transparent RGBA PNG arranged as EXACTLY FOUR columns and TWO rows, 8 isolated sprites, each in its own 256x256 cell, no visible grid. All cells face the viewer or the same slight 3/4 angle, constant head size and body proportions. Center each animal at x=128 within its cell; entire silhouette strictly inside x=24..232,y=24..228. Paws/lowest edge at y=224. No overlap across cells. Real alpha transparency; no painted checkerboard, no background color, no floor, no oval/shadow/glow, no text or labels, no props, no hand, no cursor. Motion/height will be animated in the application; do not put motion trails or duplicate silhouettes in a cell.
Character: exact Bori light oat cream rabbit, long ears peach-pink inside, tiny oval eyes, small dark nose, round little tail.
Create a gentle cartoon SCRUFF-LIFT pose, as if an invisible pointer pinches loose fur at the back of the neck. Draw a subtle lifted fold behind the head, shoulders raised and body hanging BELOW this point, front paws loose and two hind feet hanging visibly downward. Long ears relax slightly outward/backward so the whole rabbit fits the cell. This is a cute fictional animation, no injury or distress. No hand or external grabbing object.
Row 1: (0) beginning to lift, shoulders raised, paws loosen; (1) body lifting with hind legs dangling; (2) fully suspended by the SCRUFF, head slightly lowered, four paws dangling, ears soft and relaxed, round tail visible; (3) same suspended pose blinking, consistent silhouette.
Row 2: (4) first landing crouch, paws touch floor; (5) soft deeper crouch with ears tilted; (6) halfway returning to sit; (7) original neutral sitting pose from reference. Keep exact rabbit identity.
```

## puppy-dog

Reference: `Assets/Characters/puppy-dog/spritesheet.png`

```text
Use case: stylized-concept. Production animation sprite sheet for an existing desktop companion. The provided sheet is a CHARACTER IDENTITY AND STYLE REFERENCE only. Create NEW poses of exactly this same animal. Preserve its exact palette, head shape, eye and nose style, outline weight and simplicity. Soft hand-drawn 2D, rounded dark warm-brown outlines, flat soft fills. No 3D, no new markings, no clothing.
Output a 1024x512 transparent RGBA PNG arranged as EXACTLY FOUR columns and TWO rows, 8 isolated sprites, each in its own 256x256 cell, no visible grid. All cells face the viewer or the same slight 3/4 angle, constant head size and body proportions. Center each animal at x=128 within its cell; entire silhouette strictly inside x=24..232,y=24..228. Paws/lowest edge at y=224. No overlap across cells. Real alpha transparency; no painted checkerboard, no background color, no floor, no oval/shadow/glow, no text or labels, no props, no hand, no cursor. Motion/height will be animated in the application; do not put motion trails or duplicate silhouettes in a cell.
Character: exact cream puppy with warm caramel floppy ears, tiny oval eyes and brown button nose.
Create a gentle cartoon SCRUFF-LIFT pose, as if an invisible pointer pinches a little loose fur behind the head. Clearly show a small raised neck fold, with the body hanging beneath it, front paws loose and hind legs dangling down rather than sitting. Long floppy ears hang by gravity and tail curves downward. Cute relaxed surprise, no distress or injury. No human hand or grabbing object.
Row 1: (0) beginning to lift, raised shoulders; (1) body lifting and legs loosening; (2) fully suspended by scruff with all four paws dangling and floppy ears down; (3) identical held posture with a blink.
Row 2: (4) first landing crouch, feet on floor; (5) soft deeper landing crouch; (6) halfway returning to original sit; (7) original neutral sit matching reference. Preserve palette and clean simple face.
```

## penguin

Reference: `Assets/Characters/penguin/spritesheet.png`

```text
Use case: stylized-concept. Production animation sprite sheet for an existing desktop companion. The provided sheet is a CHARACTER IDENTITY AND STYLE REFERENCE only. Create NEW poses of exactly this same animal. Preserve its exact palette, head shape, eye and nose style, outline weight and simplicity. Soft hand-drawn 2D, rounded dark warm-brown outlines, flat soft fills. No 3D, no new markings, no clothing.
Output a 1024x512 transparent RGBA PNG arranged as EXACTLY FOUR columns and TWO rows, 8 isolated sprites, each in its own 256x256 cell, no visible grid. All cells face the viewer or the same slight 3/4 angle, constant head size and body proportions. Center each animal at x=128 within its cell; entire silhouette strictly inside x=24..232,y=24..228. Paws/lowest edge at y=224. No overlap across cells. Real alpha transparency; no painted checkerboard, no background color, no floor, no oval/shadow/glow, no text or labels, no props, no hand, no cursor. Motion/height will be animated in the application; do not put motion trails or duplicate silhouettes in a cell.
Character: exact little charcoal-gray and warm-cream penguin with tiny orange beak and orange feet.
Create a gentle cartoon back-of-neck LIFT pose: a subtle raised fold of feathers behind the head implies an invisible pointer lifting it. Body hangs below raised shoulders, flippers droop vertically and the two orange feet dangle downward with toes relaxed. Head leans slightly forward. Cute and comfortable, no distress or injury. No hand or external grabber.
Row 1: (0) beginning to lift, flippers loosen; (1) body lifting, feet tilt down; (2) fully suspended from back of neck, flippers dangling beside body and orange feet dangling rather than standing; (3) same suspended posture blinking.
Row 2: (4) first landing crouch, feet planted, belly slightly compressed; (5) soft deeper landing crouch; (6) returning upright; (7) original neutral stance matching reference exactly. Keep species recognizable and proportions consistent.
```

