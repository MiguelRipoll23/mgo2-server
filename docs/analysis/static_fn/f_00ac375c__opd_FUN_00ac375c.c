/* containing function of static patch target(s); entry .opd.FUN_00ac375c @ 00ac375c */

undefined8 _opd_FUN_00ac375c(void)

{
  undefined *puVar1;
  undefined4 *puVar2;
  int iVar3;
  undefined1 auStack_50 [48];
  
  puVar1 = PTR_PTR_0121bc68;
  puVar2 = (undefined4 *)_opd_FUN_008eb428(0x2ac0);
  if (puVar2 != (undefined4 *)0x0) {
    _opd_FUN_008eb598(puVar2,0);
    *puVar2 = *(undefined4 *)(puVar1 + -0x8000);
    puVar2[7] = puVar2[7] | 0x8000;
    puVar2[0x1e] = 0;
    puVar2[0x1f] = 0x5000;
    puVar2[0x1d] = 0;
    puVar2[0xaac] = 1;
    puVar2[0x27] = 0;
    puVar2[0x28] = 0;
    puVar2[0x29] = 0;
    puVar2[0x20] = 0;
    puVar2[0x21] = 0;
    puVar2[0x22] = 0;
    iVar3 = FUN_00fbac3c(0x44);
    if ((iVar3 == 0) && (iVar3 = FUN_00fbd1bc(), iVar3 == 0)) {
      iVar3 = _opd_FUN_00d723f0(*(undefined4 *)(puVar1 + -0x7ffc),0);
      puVar2[0x25] = iVar3;
      if (iVar3 != 0) {
        iVar3 = _opd_FUN_0026c2f0(auStack_50);
        if (iVar3 < 0) {
          puVar2[0x1c] = 0xe;
        }
        else {
          iVar3 = FUN_00fbd39c(2,auStack_50,*(undefined4 *)(puVar1 + -0x7ff8),puVar2,puVar2 + 0x1e);
          if ((iVar3 == 0) && (iVar3 = FUN_00fbd33c(puVar2[0x1e]), iVar3 == 0)) {
            puVar2[0x1c] = 0;
          }
        }
      }
    }
  }
  return *(undefined8 *)(puVar2 + 0xc);
}

