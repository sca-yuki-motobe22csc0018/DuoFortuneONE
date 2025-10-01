import csv
import os

# 入力と出力ファイル
csv_file = "Card_Data.csv"
output_file = "character_set.txt"

# 既存の文字セットを読み込み
old_chars = set()
if os.path.exists(output_file):
    with open(output_file, "r", encoding="utf-8") as f:
        old_chars = set(f.read())

# 新しい文字セットを収集
unique_chars = set()

with open(csv_file, "r", encoding="utf-8") as f:
    reader = csv.reader(f)
    for row in reader:
        for cell in row:
            for char in cell:
                if not char.isspace():
                    unique_chars.add(char)

# 差分を計算
new_chars = unique_chars - old_chars

# 出力
with open(output_file, "w", encoding="utf-8") as f:
    f.write("".join(sorted(unique_chars)))

print("✅ 文字セットを更新しました:", output_file)

if new_chars:
    print("✨ 新しく追加された文字:", "".join(sorted(new_chars)))
else:
    print("（新しい文字はありません）")
