"""Export the approved AI-assisted collar and blade skill icon for RimWorld 1.7."""
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent / "SSC_CombatOverdrive_Blade_Source.png"
DESTINATION = ROOT / "1.7/Textures/UI/Icons/Abilities/SSC_CombatOverdrive.png"
SIZE = 128
CONTENT_SIZE = 120


def export() -> None:
    image = Image.open(SOURCE).convert("RGBA")
    # Ignore faint generated alpha noise when locating the visible design;
    # preserve the artwork's alpha and a small antialiasing margin.
    bounds = image.getchannel("A").point(lambda value: 255 if value >= 16 else 0).getbbox()
    if bounds is None:
        raise ValueError("No visible artwork")
    left, top, right, bottom = bounds
    image = image.crop((max(0, left - 4), max(0, top - 4),
                        min(image.width, right + 4), min(image.height, bottom + 4)))
    image.thumbnail((CONTENT_SIZE, CONTENT_SIZE), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2))
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(DESTINATION, optimize=True)
    print(f"Exported {SIZE}x{SIZE} RGBA; artwork {image.width}x{image.height}")


if __name__ == "__main__":
    export()
