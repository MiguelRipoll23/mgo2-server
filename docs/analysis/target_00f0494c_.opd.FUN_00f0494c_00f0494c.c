// original call TARGET 0x00f0494c

undefined4 _opd_FUN_00f0494c(int param_1)

{
  int *piVar1;
  int iVar2;
  undefined4 uVar3;
  int iVar4;
  byte *pbVar5;
  undefined4 local_40 [6];
  
  if (param_1 == 0) {
    return 0xffffffe8;
  }
  piVar1 = (int *)_opd_FUN_00f04920(param_1);
  iVar2 = *piVar1;
  if (iVar2 == 0x4932) {
    uVar3 = _opd_FUN_00f1c208(param_1);
    return uVar3;
  }
  if (iVar2 < 0x4933) {
    if (iVar2 == 0x43cb) {
      uVar3 = _opd_FUN_00f0d4c0(param_1);
      return uVar3;
    }
    if (iVar2 < 0x43cc) {
      if (iVar2 == 0x4301) {
        uVar3 = _opd_FUN_00f0e0c0(param_1);
        return uVar3;
      }
      if (iVar2 < 0x4302) {
        if (iVar2 == 0x4124) {
          uVar3 = _opd_FUN_00f09854(param_1);
          return uVar3;
        }
        if (iVar2 < 0x4125) {
          if (iVar2 == 0x4105) {
            uVar3 = _opd_FUN_00f0b110(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4106) {
            if (iVar2 == 0x3004) {
              uVar3 = _opd_FUN_00f06210(param_1);
              return uVar3;
            }
            if (iVar2 < 0x3005) {
              if (iVar2 == 4) {
                uVar3 = _opd_FUN_00f01904(param_1,2);
                return uVar3;
              }
              if (iVar2 == 5) {
                uVar3 = _opd_FUN_00f01764(param_1,2);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4101) {
                uVar3 = _opd_FUN_00f08a18(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4103) {
                uVar3 = _opd_FUN_00f0b590(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4113) {
              uVar3 = _opd_FUN_00f072f8(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4114) {
              if (iVar2 == 0x4107) {
                uVar3 = _opd_FUN_00f0a5b4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4111) {
                uVar3 = _opd_FUN_00f073bc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4121) {
                uVar3 = _opd_FUN_00f0a0b0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4122) {
                uVar3 = _opd_FUN_00f09a48(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4120) {
                uVar3 = _opd_FUN_00f0a188(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x4142) {
            uVar3 = _opd_FUN_00f0c18c(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4143) {
            if (iVar2 == 0x4131) {
              uVar3 = _opd_FUN_00f08d9c(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4132) {
              if (iVar2 == 0x4125) {
                uVar3 = _opd_FUN_00f096ac(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4129) {
                uVar3 = _opd_FUN_00f09384(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4140) {
                uVar3 = _opd_FUN_00f0c800(param_1);
                return uVar3;
              }
              if (0x4140 < iVar2) {
                uVar3 = _opd_FUN_00f06bb8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4133) {
                uVar3 = _opd_FUN_00f09140(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4211) {
              uVar3 = _opd_FUN_00f071cc(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4212) {
              if (iVar2 == 0x4143) {
                uVar3 = _opd_FUN_00f06af4(param_1);
                return uVar3;
              }
              if ((iVar2 == 0x4151) &&
                 (piVar1 = (int *)_opd_FUN_00f04920(param_1), *piVar1 == 0x4151)) {
                _opd_FUN_00f2ddec(piVar1);
                iVar2 = _opd_FUN_00f2e20c(piVar1,local_40);
                if (iVar2 != 0) {
                  return 0xffffffb9;
                }
                _opd_FUN_00f2de00(piVar1);
                _opd_FUN_00efeb18(param_1,0x79,2);
                _opd_FUN_00efeb80(param_1,0x79,local_40[0]);
                return 0;
              }
            }
            else {
              if (iVar2 == 0x4213) {
                uVar3 = _opd_FUN_00f070d4(param_1);
                return uVar3;
              }
              if (iVar2 < 0x4213) {
                uVar3 = _opd_FUN_00f088b8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4221) {
                uVar3 = _opd_FUN_00f0a2a8(param_1);
                return uVar3;
              }
            }
          }
        }
      }
      else {
        if (iVar2 == 0x4349) {
          uVar3 = _opd_FUN_00f1ddd4(param_1);
          return uVar3;
        }
        if (iVar2 < 0x434a) {
          if (iVar2 == 0x4317) {
            uVar3 = _opd_FUN_00f12444(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4318) {
            if (iVar2 == 0x4305) {
              uVar3 = _opd_FUN_00f130bc(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4306) {
              if (iVar2 == 0x4302) {
                uVar3 = _opd_FUN_00f117f4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4303) {
                uVar3 = _opd_FUN_00f0dfc4(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4311) {
                uVar3 = _opd_FUN_00f10ee4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4313) {
                uVar3 = _opd_FUN_00f1256c(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4341) {
              uVar3 = _opd_FUN_00f106ec(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4342) {
              if (iVar2 == 0x4321) {
                uVar3 = _opd_FUN_00f12278(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4323) {
                uVar3 = _opd_FUN_00f0deb4(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4345) {
                uVar3 = _opd_FUN_00f104d4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4347) {
                uVar3 = _opd_FUN_00f103c8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4343) {
                uVar3 = _opd_FUN_00f105e0(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x43a3) {
            uVar3 = _opd_FUN_00f0d958(param_1);
            return uVar3;
          }
          if (iVar2 < 0x43a4) {
            if (iVar2 == 0x4393) {
              uVar3 = _opd_FUN_00f0dc68(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4394) {
              if (iVar2 == 0x4381) {
                uVar3 = _opd_FUN_00f0ddf0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4391) {
                uVar3 = _opd_FUN_00f0dd2c(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4399) {
                uVar3 = _opd_FUN_00f0dae0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43a1) {
                uVar3 = _opd_FUN_00f0da1c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4395) {
                uVar3 = _opd_FUN_00f0dba4(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x43b1) {
              uVar3 = _opd_FUN_00f0d7d0(param_1);
              return uVar3;
            }
            if (iVar2 < 0x43b2) {
              if (iVar2 == 0x43a5) {
                uVar3 = _opd_FUN_00f0d894(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43a7) {
                uVar3 = _opd_FUN_00f0d584(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x43c5) {
                uVar3 = _opd_FUN_00f0d648(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43c9) {
                uVar3 = _opd_FUN_00f0e578(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43c1) {
                uVar3 = _opd_FUN_00f0d70c(param_1);
                return uVar3;
              }
            }
          }
        }
      }
    }
    else {
      if (iVar2 == 0x4687) {
        uVar3 = _opd_FUN_00f06c7c(param_1);
        return uVar3;
      }
      if (iVar2 < 0x4688) {
        if (iVar2 == 0x4442) {
          uVar3 = _opd_FUN_00f235c4(param_1);
          return uVar3;
        }
        if (iVar2 < 0x4443) {
          if (iVar2 == 0x43f1) {
            uVar3 = _opd_FUN_00f2cf30(param_1);
            return uVar3;
          }
          if (iVar2 < 0x43f2) {
            if (iVar2 == 0x43e3) {
              uVar3 = _opd_FUN_00f2c968(param_1);
              return uVar3;
            }
            if (iVar2 < 0x43e4) {
              if (iVar2 == 0x43d1) {
                uVar3 = _opd_FUN_00f0844c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43e1) {
                uVar3 = _opd_FUN_00f2cdfc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x43e4) {
                uVar3 = _opd_FUN_00f2cc2c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43f0) {
                uVar3 = _opd_FUN_00f2c6cc(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x43f4) {
              uVar3 = _opd_FUN_00f2c488(param_1);
              return uVar3;
            }
            if (iVar2 < 0x43f5) {
              if (iVar2 == 0x43f2) {
                uVar3 = _opd_FUN_00f2c5b4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43f3) {
                uVar3 = _opd_FUN_00f2c4fc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4401) {
                uVar3 = _opd_FUN_00f238f4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4441) {
                uVar3 = _opd_FUN_00f236cc(param_1);
                return uVar3;
              }
              if (iVar2 == 0x43f5) {
                uVar3 = _opd_FUN_00f2c3dc(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x4602) {
            uVar3 = _opd_FUN_00f13c84(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4603) {
            if (iVar2 == 0x4581) {
              uVar3 = _opd_FUN_00f146ec(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4582) {
              if (iVar2 == 0x4502) {
                uVar3 = _opd_FUN_00f14e3c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4512) {
                uVar3 = _opd_FUN_00f1488c(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4583) {
                uVar3 = _opd_FUN_00f14320(param_1);
                return uVar3;
              }
              if (iVar2 < 0x4583) {
                uVar3 = _opd_FUN_00f1450c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4601) {
                uVar3 = _opd_FUN_00f13b3c(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4682) {
              uVar3 = _opd_FUN_00f08738(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4683) {
              if (iVar2 == 0x4603) {
                uVar3 = _opd_FUN_00f13a40(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4681) {
                uVar3 = _opd_FUN_00f06fa4(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4685) {
                uVar3 = _opd_FUN_00f06d78(param_1);
                return uVar3;
              }
              if (0x4685 < iVar2) {
                uVar3 = _opd_FUN_00f08568(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4683) {
                uVar3 = _opd_FUN_00f06ea8(param_1);
                return uVar3;
              }
            }
          }
        }
      }
      else {
        if (iVar2 == 0x4909) {
          uVar3 = _opd_FUN_00f16100(param_1);
          return uVar3;
        }
        if (iVar2 < 0x490a) {
          if (iVar2 == 0x4841) {
            uVar3 = _opd_FUN_00f242e4(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4842) {
            if (iVar2 == 0x4802) {
              uVar3 = _opd_FUN_00f24ddc(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4803) {
              if (iVar2 == 0x4701) {
                piVar1 = (int *)_opd_FUN_00f04920(param_1);
                if (*piVar1 == 0x4701) {
                  _opd_FUN_00f2ddec(piVar1);
                  iVar2 = _opd_FUN_00f2e20c(piVar1,local_40);
                  if (iVar2 != 0) {
                    return 0xffffffb9;
                  }
                  _opd_FUN_00f2de00(piVar1);
                  _opd_FUN_00efeb18(param_1,0x78,2);
                  _opd_FUN_00efeb80(param_1,0x78,local_40[0]);
                  return 0;
                }
              }
              else if (iVar2 == 0x4801) {
                uVar3 = _opd_FUN_00f24a68(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4822) {
                uVar3 = _opd_FUN_00f243f8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4823) {
                uVar3 = _opd_FUN_00f23ca8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4821) {
                uVar3 = _opd_FUN_00f245a0(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4901) {
              uVar3 = _opd_FUN_00f15524(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4902) {
              if (iVar2 == 0x4861) {
                uVar3 = _opd_FUN_00f23db0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4881) {
                uVar3 = _opd_FUN_00f23bd4(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4903) {
                uVar3 = _opd_FUN_00f1542c(param_1);
                return uVar3;
              }
              if (iVar2 < 0x4903) {
                uVar3 = _opd_FUN_00f15b30(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4905) {
                uVar3 = _opd_FUN_00f16850(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x491c) {
            uVar3 = _opd_FUN_00f1ca2c(param_1);
            return uVar3;
          }
          if (iVar2 < 0x491d) {
            if (iVar2 == 0x4915) {
              _opd_FUN_00f18db4(param_1);
              return 0;
            }
            if (iVar2 < 0x4916) {
              if (iVar2 == 0x4911) {
                _opd_FUN_00f19f4c(param_1);
                return 0;
              }
              if (iVar2 == 0x4913) {
                uVar3 = _opd_FUN_00f19dfc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4919) {
                uVar3 = _opd_FUN_00f1c5a0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x491a) {
                uVar3 = _opd_FUN_00f1c4a8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4918) {
                uVar3 = _opd_FUN_00f1c6dc(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4922) {
              uVar3 = _opd_FUN_00f1c06c(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4923) {
              if (iVar2 == 0x491d) {
                uVar3 = _opd_FUN_00f1c3b0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4921) {
                uVar3 = _opd_FUN_00f17e94(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4925) {
                uVar3 = _opd_FUN_00f1be80(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4931) {
                uVar3 = _opd_FUN_00f17f58(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4924) {
                uVar3 = _opd_FUN_00f17dd0(param_1);
                return uVar3;
              }
            }
          }
        }
      }
    }
  }
  else {
    if (iVar2 == 0x4a34) {
      uVar3 = _opd_FUN_00f230c4(param_1);
      return uVar3;
    }
    if (iVar2 < 0x4a35) {
      if (iVar2 == 0x49c3) {
        uVar3 = _opd_FUN_00f1d42c(param_1);
        return uVar3;
      }
      if (iVar2 < 0x49c4) {
        if (iVar2 == 0x4983) {
          uVar3 = _opd_FUN_00f18068(param_1);
          return uVar3;
        }
        if (iVar2 < 0x4984) {
          if (iVar2 == 0x4961) {
            uVar3 = _opd_FUN_00f1b718(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4962) {
            if (iVar2 == 0x4943) {
              uVar3 = _opd_FUN_00f1af74(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4944) {
              if (iVar2 == 0x4941) {
                uVar3 = _opd_FUN_00f17d0c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4942) {
                uVar3 = _opd_FUN_00f1bcfc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4950) {
                uVar3 = _opd_FUN_00f1ba60(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4960) {
                uVar3 = _opd_FUN_00f1b8f8(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4966) {
              uVar3 = _opd_FUN_00f1b3b8(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4967) {
              if (iVar2 == 0x4964) {
                uVar3 = _opd_FUN_00f1b61c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4965) {
                uVar3 = _opd_FUN_00f1b4dc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4981) {
                uVar3 = _opd_FUN_00f18170(param_1);
                return uVar3;
              }
              if (0x4981 < iVar2) {
                uVar3 = _opd_FUN_00f19fbc(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4967) {
                uVar3 = _opd_FUN_00f1b198(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x49a8) {
            uVar3 = _opd_FUN_00f1ae68(param_1);
            return uVar3;
          }
          if (iVar2 < 0x49a9) {
            if (iVar2 == 0x4991) {
              uVar3 = _opd_FUN_00f15e24(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4992) {
              if (iVar2 == 0x4985) {
                uVar3 = _opd_FUN_00f19edc(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4987) {
                uVar3 = _opd_FUN_00f1c8c0(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x49a1) {
                uVar3 = _opd_FUN_00f19d8c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x49a2) {
                uVar3 = _opd_FUN_00f1abf4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4993) {
                uVar3 = _opd_FUN_00f16fc4(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x49b6) {
              uVar3 = _opd_FUN_00f1a5c0(param_1);
              return uVar3;
            }
            if (iVar2 < 0x49b7) {
              if (iVar2 == 0x49b1) {
                uVar3 = _opd_FUN_00f19e6c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x49b5) {
                uVar3 = _opd_FUN_00f1a698(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x49c0) {
                uVar3 = _opd_FUN_00f1d9ec(param_1);
                return uVar3;
              }
              if (iVar2 == 0x49c1) {
                uVar3 = _opd_FUN_00f1d700(param_1);
                return uVar3;
              }
              if (iVar2 == 0x49b7) {
                uVar3 = _opd_FUN_00f1a4e8(param_1);
                return uVar3;
              }
            }
          }
        }
      }
      else {
        if (iVar2 == 0x4a12) {
          uVar3 = _opd_FUN_00f229bc(param_1);
          return uVar3;
        }
        if (iVar2 < 0x4a13) {
          if (iVar2 == 0x49f0) {
            uVar3 = _opd_FUN_00f2dc28(param_1);
            return uVar3;
          }
          if (iVar2 < 0x49f1) {
            if (iVar2 == 0x49d2) {
              uVar3 = _opd_FUN_00f1aa74(param_1);
              return uVar3;
            }
            if (iVar2 < 0x49d3) {
              if (iVar2 == 0x49c9) {
                uVar3 = _opd_FUN_00f1d2cc(param_1);
                return uVar3;
              }
              if (iVar2 == 0x49d1) {
                uVar3 = _opd_FUN_00f17b84(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x49e1) {
                uVar3 = _opd_FUN_00f17ac0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x49e2) {
                uVar3 = _opd_FUN_00f1a770(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4a02) {
              uVar3 = _opd_FUN_00f205c4(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4a03) {
              if (iVar2 == 0x4a00) {
                uVar3 = _opd_FUN_00f21a5c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a01) {
                uVar3 = _opd_FUN_00f20bfc(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4a10) {
                uVar3 = _opd_FUN_00f22ea0(param_1);
                return uVar3;
              }
              if (0x4a10 < iVar2) {
                uVar3 = _opd_FUN_00f22c44(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a03) {
                uVar3 = _opd_FUN_00f22544(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x4a26) {
            uVar3 = _opd_FUN_00f1e7c0(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4a27) {
            if (iVar2 == 0x4a21) {
              uVar3 = _opd_FUN_00f22328(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4a22) {
              if (iVar2 == 0x4a13) {
                uVar3 = _opd_FUN_00f11d58(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a20) {
                uVar3 = _opd_FUN_00f22688(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4a23) {
                uVar3 = _opd_FUN_00f21950(param_1);
                return uVar3;
              }
              if (iVar2 < 0x4a23) {
                uVar3 = _opd_FUN_00f220a0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a24) {
                uVar3 = _opd_FUN_00f20034(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4a29) {
              uVar3 = _opd_FUN_00f214ec(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4a2a) {
              if (iVar2 == 0x4a27) {
                uVar3 = _opd_FUN_00f203a8(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a28) {
                uVar3 = _opd_FUN_00f21738(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4a32) {
                uVar3 = _opd_FUN_00f234a4(param_1);
                return uVar3;
              }
              if (0x4a32 < iVar2) {
                uVar3 = _opd_FUN_00f23220(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a31) {
                uVar3 = _opd_FUN_00f1ffb4(param_1);
                return uVar3;
              }
            }
          }
        }
      }
    }
    else {
      if (iVar2 == 0x4b65) {
        uVar3 = _opd_FUN_00f27628(param_1);
        return uVar3;
      }
      if (iVar2 < 0x4b66) {
        if (iVar2 == 0x4b33) {
          uVar3 = _opd_FUN_00f266c4(param_1);
          return uVar3;
        }
        if (iVar2 < 0x4b34) {
          if (iVar2 == 0x4b01) {
            uVar3 = _opd_FUN_00f27a18(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4b02) {
            if (iVar2 == 0x4a43) {
              uVar3 = _opd_FUN_00f1e884(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4a44) {
              if (iVar2 == 0x4a41) {
                uVar3 = _opd_FUN_00f1e98c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4a42) {
                uVar3 = _opd_FUN_00f2082c(param_1);
                return uVar3;
              }
            }
            else if (iVar2 == 0x4a47) {
              piVar1 = (int *)_opd_FUN_00f04920(param_1);
              if (*piVar1 == 0x4a47) {
                iVar2 = *(int *)(param_1 + 0x294a4);
                if (iVar2 == 0) {
                  return 0;
                }
                pbVar5 = (byte *)(iVar2 + 0x31034);
                *(undefined2 *)(iVar2 + 0x31038) = 0;
                pbVar5[0] = 0;
                pbVar5[1] = 0;
                *(undefined2 *)(iVar2 + 0x31036) = 0;
                _opd_FUN_00f2ddec(piVar1);
                iVar4 = _opd_FUN_00f2e134(piVar1,local_40);
                if (iVar4 != 0) {
                  return 0xffffffb9;
                }
                if (0xb < local_40[0]._0_1_) {
                  return 0xffffffb9;
                }
                *pbVar5 = local_40[0]._0_1_;
                iVar4 = _opd_FUN_00f2e134(piVar1,iVar2 + 0x31035);
                if (iVar4 != 0) {
                  return 0xffffffb9;
                }
                iVar4 = _opd_FUN_00f2e134(piVar1,iVar2 + 0x31036);
                if (iVar4 != 0) {
                  return 0xffffffb9;
                }
                iVar2 = _opd_FUN_00f2e1bc(piVar1,iVar2 + 0x31038);
                if (iVar2 != 0) {
                  return 0xffffffb9;
                }
                _opd_FUN_00f2de00(piVar1);
                _opd_FUN_00effa0c(param_1,0x2c,local_40[0]._0_1_);
                return 0;
              }
            }
            else if (iVar2 == 0x4a50) {
              uVar3 = _opd_FUN_00f200dc(param_1);
              return uVar3;
            }
          }
          else {
            if (iVar2 == 0x4b12) {
              uVar3 = _opd_FUN_00f26d58(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4b13) {
              if (iVar2 == 0x4b05) {
                uVar3 = _opd_FUN_00f27930(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b11) {
                uVar3 = _opd_FUN_00f264e8(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4b21) {
                uVar3 = _opd_FUN_00f29508(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b31) {
                uVar3 = _opd_FUN_00f26788(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b13) {
                uVar3 = _opd_FUN_00f263e0(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x4b4d) {
            uVar3 = _opd_FUN_00f2abc0(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4b4e) {
            if (iVar2 == 0x4b43) {
              uVar3 = _opd_FUN_00f2783c(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4b44) {
              if (iVar2 == 0x4b37) {
                uVar3 = _opd_FUN_00f25ea4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b41) {
                uVar3 = _opd_FUN_00f276f8(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4b49) {
                uVar3 = _opd_FUN_00f27b80(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b4b) {
                uVar3 = _opd_FUN_00f2acdc(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b47) {
                uVar3 = _opd_FUN_00f290b8(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4b54) {
              uVar3 = _opd_FUN_00f28a6c(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4b55) {
              if (iVar2 == 0x4b51) {
                uVar3 = _opd_FUN_00f2631c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b53) {
                uVar3 = _opd_FUN_00f261ec(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4b61) {
                uVar3 = _opd_FUN_00f2602c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b63) {
                uVar3 = _opd_FUN_00f25f68(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b55) {
                uVar3 = _opd_FUN_00f260f0(param_1);
                return uVar3;
              }
            }
          }
        }
      }
      else {
        if (iVar2 == 0x4e12) {
          uVar3 = _opd_FUN_00f2b7b4(param_1);
          return uVar3;
        }
        if (iVar2 < 0x4e13) {
          if (iVar2 == 0x4b81) {
            uVar3 = _opd_FUN_00f299cc(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4b82) {
            if (iVar2 == 0x4b72) {
              uVar3 = _opd_FUN_00f29ca0(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4b73) {
              if (iVar2 == 0x4b67) {
                uVar3 = _opd_FUN_00f27558(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b71) {
                uVar3 = _opd_FUN_00f2a750(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4b75) {
                uVar3 = _opd_FUN_00f26b88(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b76) {
                uVar3 = _opd_FUN_00f25c78(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b74) {
                uVar3 = _opd_FUN_00f25d74(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4b93) {
              uVar3 = _opd_FUN_00f259f8(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4b94) {
              if (iVar2 == 0x4b91) {
                uVar3 = _opd_FUN_00f25b0c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4b92) {
                uVar3 = _opd_FUN_00f2684c(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4e10) {
                uVar3 = _opd_FUN_00f2bbac(param_1);
                return uVar3;
              }
              if (0x4e10 < iVar2) {
                uVar3 = _opd_FUN_00f2b8f0(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4d00) {
                uVar3 = _opd_FUN_00f1f0c8(param_1);
                return uVar3;
              }
            }
          }
        }
        else {
          if (iVar2 == 0x4f09) {
            uVar3 = _opd_FUN_00f1ff34(param_1);
            return uVar3;
          }
          if (iVar2 < 0x4f0a) {
            if (iVar2 == 0x4e22) {
              uVar3 = _opd_FUN_00f2b210(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4e23) {
              if (iVar2 == 20000) {
                uVar3 = _opd_FUN_00f2c144(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4e21) {
                uVar3 = _opd_FUN_00f2b420(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4f01) {
                uVar3 = _opd_FUN_00f1e6fc(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4f03) {
                uVar3 = _opd_FUN_00f1e638(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4e23) {
                uVar3 = _opd_FUN_00f2aff0(param_1);
                return uVar3;
              }
            }
          }
          else {
            if (iVar2 == 0x4f11) {
              uVar3 = _opd_FUN_00f212d8(param_1);
              return uVar3;
            }
            if (iVar2 < 0x4f12) {
              if (iVar2 == 0x4f0a) {
                uVar3 = _opd_FUN_00f21f6c(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4f10) {
                uVar3 = _opd_FUN_00f21408(param_1);
                return uVar3;
              }
            }
            else {
              if (iVar2 == 0x4f16) {
                uVar3 = _opd_FUN_00f1ef20(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4f18) {
                uVar3 = _opd_FUN_00f1e4c4(param_1);
                return uVar3;
              }
              if (iVar2 == 0x4f14) {
                uVar3 = _opd_FUN_00f1edf8(param_1);
                return uVar3;
              }
            }
          }
        }
      }
    }
  }
  return 0xffffffba;
}

