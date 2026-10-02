#!/usr/bin/env python3
"""Records the animated diagrams into GIFs, plus a still PNG of the first frame.

Not in real time: every CSS animation is paused at the frame's time and the
page is captured, so the result is the same on any machine. Needs Playwright
(with the system Chrome) and ffmpeg on the PATH.

    python tools/record.py                 # every diagram
    python tools/record.py circuito_en     # just one
"""

from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parent.parent
DIAGRAMS = ROOT / "docs" / "diagrams"
MEDIA = ROOT / "docs" / "media"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
FPS = 12
WIDTH = 1200

# One second per step is enough to read the GIF; the HTML keeps its own pace.
SPEED = 1.0


def cycle_seconds(page) -> float:
    return page.evaluate("Math.max(...document.getAnimations().map(a => a.effect.getComputedTiming().duration)) / 1000")


def record(page, html: Path) -> None:
    page.goto(html.as_uri())
    width, height = page.evaluate("document.querySelector('svg').viewBox.baseVal.width"), page.evaluate("document.querySelector('svg').viewBox.baseVal.height")
    page.set_viewport_size({"width": int(width), "height": int(height)})
    page.wait_for_timeout(300)
    cycle = cycle_seconds(page)
    frames = int(cycle * FPS / SPEED)
    with tempfile.TemporaryDirectory() as tmp:
        for i in range(frames):
            ms = i * 1000 / FPS * SPEED
            page.evaluate(f"document.getAnimations().forEach(a => {{ a.pause(); a.currentTime = {ms}; }})")
            page.locator("svg").screenshot(path=f"{tmp}/f{i:04d}.png")
        gif = MEDIA / f"{html.stem}.gif"
        subprocess.run(
            [
                "ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", f"{tmp}/f%04d.png",
                "-vf", "split[a][b];[a]palettegen=max_colors=128:stats_mode=full[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle",
                "-loop", "0", str(gif),
            ],
            check=True,
        )
        shutil.copy(f"{tmp}/f0000.png", MEDIA / f"{html.stem}.png")
    print(f"{gif.name}: {frames} frames, {gif.stat().st_size / 1024:.0f} KB")


def main() -> None:
    MEDIA.mkdir(parents=True, exist_ok=True)
    only = set(sys.argv[1:])
    pages = sorted(p for p in DIAGRAMS.glob("*.html") if not only or p.stem in only)
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=CHROME)
        page = browser.new_page(viewport={"width": WIDTH, "height": 750}, device_scale_factor=1)
        for html in pages:
            record(page, html)
        browser.close()


if __name__ == "__main__":
    main()
