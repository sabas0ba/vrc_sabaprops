#!/usr/bin/env python3
"""Unity の Screen FX キャプチャから、ローカル比較用 HTML を生成する。"""

import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("directory", type=Path, help="PNG キャプチャの出力ディレクトリ")
args = parser.parse_args()
directory = args.directory
presets = sorted(path.name.removesuffix("-Standard.png") for path in directory.glob("*-Standard.png"))
if not presets or not (directory / "Baseline.png").is_file():
    parser.error("Standard の画像と Baseline.png が必要です")
for preset in presets:
    if not (directory / f"{preset}-Lite.png").is_file():
        parser.error(f"{preset}-Lite.png がありません")

page = """<!doctype html>
<html lang="ja"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Screen FX 描画比較</title>
<style>
body{font:16px system-ui,sans-serif;background:#15191f;color:#e5eaf0;margin:24px}
h1{font-size:26px}p{line-height:1.7;max-width:85ch}a{color:#96c9ff}
header{position:sticky;top:0;background:#15191ff2;padding:12px 0;z-index:2}
label{display:inline-block;margin:8px 24px 8px 0}select{font:inherit;padding:6px}
main{display:grid;grid-template-columns:repeat(auto-fit,minmax(270px,1fr));gap:20px}
figure{margin:0;background:#222a35;border-radius:8px;padding:12px}
figcaption{font-weight:600;margin-bottom:8px}.frame{position:relative;aspect-ratio:1}
img{display:block;width:100%;height:100%}.effect{position:absolute;inset:0;clip-path:inset(0 0 0 var(--split,0%))}
</style>
<h1>Screen FX 描画比較</h1>
<p>Unity 2022.3.22f1 / Direct3D 11 の Camera.Render による 512×512 PNG。
共通のカラーパターンに効果を適用しています。VRChat のワールド・両眼表示の撮影ではありません。
左側が効果なし、右側が選択した版です。画像をクリックすると PNG を開きます。
動きのある効果は撮影時点の静止画です。</p>
<header><label>版 <select id="mode"><option value="Standard">通常版 / GrabPass</option>
<option value="Lite">Lite</option></select></label>
<label>比較境界 <input id="split" type="range" min="0" max="100" value="0"></label>
<a href="Baseline.png">効果なしの画像</a></header><main id="gallery"></main>
<script>
const presets=PRESET_DATA;
const mode=document.querySelector('#mode');
const gallery=document.querySelector('#gallery');
function render(){
  gallery.replaceChildren();
  for(const name of presets){
    const figure=document.createElement('figure');
    const caption=document.createElement('figcaption');caption.textContent=name;
    const link=document.createElement('a');link.className='frame';link.style.display='block';
    link.href=name+'-'+mode.value+'.png';
    const base=document.createElement('img');base.src='Baseline.png';base.alt='効果なし';
    const effect=document.createElement('img');effect.src=link.href;effect.alt=name+' '+mode.value;
    effect.className='effect';link.append(base,effect);figure.append(caption,link);gallery.append(figure);
  }
}
mode.addEventListener('change',render);
document.querySelector('#split').addEventListener('input',event=>{
  document.documentElement.style.setProperty('--split',event.target.value+'%');
});
render();
</script></html>
"""
page = page.replace("PRESET_DATA", json.dumps(presets).replace("<", "\\u003c"))
(directory / "index.html").write_text(page, encoding="utf-8")
print(f"wrote {directory / 'index.html'}: {len(presets)} presets, Standard / Lite")
