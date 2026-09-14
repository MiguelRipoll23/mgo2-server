// original call TARGET 0x000cdc90

int _opd_FUN_000cdc90(byte *param_1)

{
  byte bVar1;
  ulonglong uVar2;
  ulonglong uVar3;
  
  bVar1 = *param_1;
  if (bVar1 != 0) {
    uVar2 = 0;
    do {
      uVar3 = (ulonglong)bVar1;
      param_1 = param_1 + 1;
      bVar1 = *param_1;
      uVar2 = (uVar2 << 5 | uVar2 >> 0x13) + uVar3 & 0xffffff;
    } while (bVar1 != 0);
    if ((int)uVar2 != 0) {
      return (int)uVar2;
    }
  }
  return 1;
}

