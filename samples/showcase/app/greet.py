from dataclasses import dataclass


@dataclass
class Greeting:
    name: str
    excited: bool = False

    def render(self) -> str:
        mark = "!" if self.excited else "."
        return f"Hello, {self.name}{mark}"


if __name__ == "__main__":
    for name in ("scenetake", "terminal"):
        print(Greeting(name, excited=True).render())
