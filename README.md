# Interaktywne środowisko do projektowania i symulacji deterministycznych automatów skończonych

### Przykład

<div align="center">
  <img src="./example.png" alt="Przykład" width="80%">
</div>


Rysunek przedstawia prosty automat akceptujący język $L$ nad alfabetem $\Sigma = {0, 1}$, składający się z ciągów binarnych o parzystej liczbie zer. Automat zaakceptuje słowo 100, natomiast odrzuci słowo 10100.

## Opis aplikacji

Aplikacja składa się z dwóch części:

1. **Lab:**  Edytora automatów, który pozwala na tworzenie automatu oraz jego eksport/import z pliku.
2. **Home:** Środowiska uruchomieniowego, które umożliwia import automatu z pliku oraz symulację obliczeń na zadanym słowie wejściowym.
   
Przedmiotem zadania laboratoryjnego będzie **uproszczony edytor automatów**. Część domowa projektu obejmuje rozszerzenie funkcjonalności edytora oraz implementację środowiska uruchomieniowego.

### Edytor automatów - zadanie laboratoryjne

Aplikacja składa się z interaktywnego obszaru, służącego do rysowania automatu, a także elementów UI, za pomocą których można modyfikować automat.

Funkcjonalności edytora:

- **Dodawanie stanów:** (1.5p)
  - Dwukrotne kliknięcie lewym przyciskiem myszki w pustym obszarze płótna powoduje dodanie nowego stanu w miejscu kursora.
  - Stan reprezentowany jest jako okrąg z etykietą (patrz [Przykład](#przykład)).
  - Stany są automatycznie numerowane kolejnymi liczbami naturalnymi (np. $q_0$, $q_1$, ..., $q_n$).
- **Aktywacja stanu:** (1p)
  - Pojedyncze kliknięcie na stanie powoduje jego aktywację.
  - Aktywny element powinien być wyróżniony wizualnie (np. poprzez zmianę koloru obramowania).
  - Kliknięcie w obszar rysowania, na którym nie ma stanu zdejmuje wyróżnienie z poprzednio aktywnego stanu.
- **Zmiana położenia stanu:** (2p)
  - Wciśnięcie lewego przycisku myszki na stanie, przytrzymanie go i ruch myszką powodują zmianę położenia stanu.
  - Upuszczenie stanu następuje po zwolnieniu przycisku.
- **Edycja stanu:** (2p)
  - Przycisk "Usuń stan" powoduje aktywnego stanu. Przycisk jest aktywny tylko wtedy, gdy wybrany jest aktywny stan.
  - Dostępne są Checkboxy "Oznacz jako akceptujący" oraz "Oznacz jako początkowy". Checkboxy są aktywne tylko wtedy, gdy wybrany jest aktywny stan.
  - Stan akceptujący wyróżnia się na tle pozostałych stanów (przykładowa realizacja to podwójny okrąg, patrz [Przykład](#przykład)).
  - Stan początkowy wyróżnia się na tle pozostałych stanów.
  - Każdy poprawny automat powinien mieć **dokładnie jeden** stan początkowy (domyślnie może nim być pierwszy utworzony stan).
  - Oznaczenie innego stanu jako początkowy automatycznie zdejmuje to oznaczenie z poprzedniego stanu początkowego.
- **Dodawanie przejścia:** (1.5p)
  - Formularz dodawania przejścia składa się z dwóch rozwijanych list, na których znajdują się stany automatu.
  - Przycisk "Dodaj przejście" dodaje przejście pomiędzy wybranymi stanami.
  - Przejście jest reprezentowane jako linia prosta pomiędzy stanami.
  - W tym etapie "Stan początkowy" musi być inny niż "Stan końcowy" (wybranie dwóch takich samych stanów i kliknięcie przycisku nie ma żadnego efektu).

## Wskazówki

- Wykorzystaj klasy zaimplementowane w Model.cs.
- Zdarzenia: MouseLeftButtonDown, MouseLeftButtonUp, MouseMove.
- Płótno: [ItemsControl](https://wpf-tutorial.com/list-controls/itemscontrol/), ItemsPanelTemplate, [Canvas](https://wpf-tutorial.com/panels/canvas/).
- Rysowanie stanu: ItemTemplate, DataTemplate, DataTemplate.Triggers.
- Kształty: Line, [Ellipse](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-draw-an-ellipse-or-a-circle).
- Kontrolki: [CheckBox](https://wpf-tutorial.com/basic-controls/the-checkbox-control/), [ComboBox](https://wpf-tutorial.com/list-controls/combobox-control/).