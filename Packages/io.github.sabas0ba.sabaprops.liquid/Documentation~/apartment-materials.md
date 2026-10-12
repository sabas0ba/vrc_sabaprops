# Apartment の生成画像と材質

Apartment の色テクスチャは、2026-10-12 に内蔵 imagegen ツールで生成しました。外部の配布素材、モデル、HDRI、追加ライブラリは使用していません。画像を取得する API への接続は、サンプルの生成・実行には必要ありません。

## ファイル

画像はパッケージの `Textures/Apartment/` に収録しています。Unity のインポート上限は 1024、MipMap と異方性フィルタを有効にしています。生成した PNG は加工せず保存しています。

| ファイル | 用途 | SHA-256 |
| --- | --- | --- |
| Floor.png | 木床 | `33A54AFC6483096D8C698ABD0B9A283C69F409788011C83D9C7E0411AFDB692A` |
| Linen.png | ソファ・ラグ・カーテン | `1E66EAC3B5401AD656473D28897210F1F5017D13E05FC75C0B656DBC1E1BEB6F` |
| Oak.png | 木製家具 | `60ED6FA866DF0EF12968D6913E732CAC4EF3993B09FBFC156D3AD59A36329CB8` |
| Stone.png | 石目タイル・天板・壁の微細模様 | `8049D1988C3F66814A157F94DA322368F59D52C99518B90885769791551ACCD2` |

## 材質の構成と制約

Unity Standard の色・法線・滑らかさ・金属度を使用します。画像は色として使い、法線は木目方向の微細な溝、布の織り、石の細かな凹凸を数式で生成しています。法線と粗さは測定値ではなく、生成画像の正確な高さを復元したものでもありません。画像の継ぎ目の完全な連続性も保証しません。

家具のメッシュは面取りと実寸基準の UV を持ちます。室内の反射はシーン生成時に Unity 標準機能でベイクします。実行時に Reflection Probe を毎フレーム更新しません。反射には初期の照明状態が残り、動的な液体や照明切替は反映されません。

これは実写計測に基づく建築ビジュアライゼーションではなく、リアル寄りの材質と形状で液体を比較するサンプルです。VRChat の画質設定・HMD・ミラーを含む実行条件によって見え方と負荷は変わります。

## 生成プロンプト

以下は内蔵 imagegen に渡したプロンプトです。CLI/API のフォールバックは使用していません。

### Oak.png

Use case: photorealistic-natural. Asset type: square seamless tileable base-color texture for a Unity Standard material, not a rendered scene. Generate natural pale European oak wood grain, straight vertical grain with fine pores and a few very subtle growth rings. One continuous wood surface, no board seams. Warm light beige honey tone, understated upscale Japanese apartment interior. Perfect orthographic scan, flat diffuse cross-polarized light, no directional lighting, no specular highlights, no shadows, no vignette, no objects, no text. Texture fills entire square image edge to edge, continuous periodic boundaries on both axes. Fine realistic wood fibres suitable for furniture and wood flooring at about 1 metre texture scale.

### Linen.png

Use case: photorealistic-natural. Asset type: square seamless tileable base-color texture, single textile material for a Unity apartment sofa. Generate an extreme close-up orthographic scan of fine natural linen upholstery, undyed neutral ivory grey-beige with densely woven tiny irregular yarns. Entire frame is flat stretched cloth, no folds, no borders, no objects. Physically realistic fibres but very restrained contrast, no dramatic shadows, flat cross-polarized lighting without highlights or baked directional illumination, no vignette. Periodic seamless edges. Do not show a sofa or room; only the fabric base color texture. No text, logos, watermarks.

### Stone.png

Use case: photorealistic-natural. Asset type: square seamless base-color surface texture for realistic interior porcelain tile and stone kitchen counters in Unity. Generate a perfectly flat orthographic material scan of warm ivory limestone-look porcelain. Very fine mineral mottling with low contrast, tiny subtle speckles, creamy pale grey beige. One continuous unbroken surface, no tile divisions or grout, no large veins, no cracks. Flat diffuse cross-polarized illumination, no directional shadows, no specular highlights, no vignette. Edge-to-edge square seamless repeatable texture. No objects, no room, no text, no watermark. The texture must remain restrained enough for colored liquid stains to be clearly visible on it.

### Floor.png

Use case: photorealistic-natural. Asset type: square seamless repeatable albedo/base-color texture of a modern Japanese apartment's pale oak plank flooring. Orthographic flat material scan, no perspective. Exactly six narrow horizontal rows of long wood boards across a square two-metre patch, staggered butt joints, tiny dark fine joints, pale natural oak grain flowing horizontally along boards. Subtle natural variation between boards, lightly oiled satin wood but no rendered reflections or highlights. Uniform diffuse cross-polarized light, no baked shadows, no vignette, no furniture or objects. Seamlessly periodic left/right and top/bottom boundaries. No text or watermark. Photographic material detail, restrained beige palette.
