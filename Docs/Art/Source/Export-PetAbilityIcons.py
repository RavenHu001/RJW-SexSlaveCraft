"""Export the approved AI-assisted pet ability artwork as transparent UI icons."""
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
DESTINATION = ROOT / "1.7/Textures/UI/Icons/Abilities"
SIZE = 128
CONTENT_SIZES = {"SSC_PetDogTame": 120, "SSC_PetCatComfort": 108}


def export(name: str) -> None:
    image = Image.open(SOURCE / f"{name}_Source.png").convert("RGBA")
    # Locate the visible artwork rather than faint generated alpha noise.
    # Keep the original alpha and a small antialiasing margin around the crop.
    bounds = image.getchannel("A").point(lambda value: 255 if value >= 16 else 0).getbbox()
    if bounds is None:
        raise ValueError(f"No visible artwork: {name}")
    left, top, right, bottom = bounds
    image = image.crop((max(0, left - 4), max(0, top - 4),
                        min(image.width, right + 4), min(image.height, bottom + 4)))
    content_size = CONTENT_SIZES[name]
    image.thumbnail((content_size, content_size), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    # Unmasked paste retains straight alpha, without applying alpha twice.
    canvas.paste(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2))
    DESTINATION.mkdir(parents=True, exist_ok=True)
    canvas.save(DESTINATION / f"{name}.png", optimize=True)
    print(f"Exported {name}: {SIZE}x{SIZE}, artwork {image.width}x{image.height}")


if __name__ == "__main__":
    for ability in ("SSC_PetDogTame", "SSC_PetCatComfort"):
        export(ability)
