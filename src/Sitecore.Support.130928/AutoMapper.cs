using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Data;
using Sitecore.Forms.Mvc.Helpers;
using Sitecore.Forms.Mvc.Interfaces;
using Sitecore.Forms.Mvc.Models;
using Sitecore.Forms.Mvc.Reflection;
using Sitecore.Forms.Mvc.ViewModels;
using Sitecore.Mvc.Extensions;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Data;

namespace Sitecore.Support.Forms.Mvc.Services
{
    public class AutoMapper : IAutoMapper<FormModel, FormViewModel>
    {
        protected FieldViewModel GetFieldViewModel(IFieldItem item, FormViewModel formViewModel)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(formViewModel, "formViewModel");
            var mVcClass = item.MVCClass;
            if (string.IsNullOrEmpty(mVcClass))
            {
                return new FieldViewModel { Item = item.InnerItem };
            }
            var type = Type.GetType(mVcClass);
            if (type == null)
            {
                return new FieldViewModel { Item = item.InnerItem };
            }
            var obj2 = Activator.CreateInstance(type);
            var model3 = obj2 as FieldViewModel;
            if (model3 == null)
            {
                Log.Warn($"[WFFM]Unable to create instance of type {mVcClass}", this);
                return null;
            }
            model3.Title = item.Title ?? string.Empty;
            model3.Name = item.Name ?? string.Empty;
            model3.Visible = true;
            model3.IsRequired = item.IsRequired;
            model3.ShowTitle = true;
            model3.Item = item.InnerItem;
            model3.FormId = formViewModel.Item.ID.ToString();
            model3.FormType = formViewModel.FormType;
            model3.FieldItemId = item.ID.ToString();
            model3.LeftColumnStyle = formViewModel.LeftColumnStyle;
            model3.RightColumnStyle = formViewModel.RightColumnStyle;
            model3.ShowInformation = true;
            var parametersDictionary = item.ParametersDictionary;
            parametersDictionary.AddRange(item.LocalizedParametersDictionary);
            model3.Parameters = parametersDictionary;
            ReflectionUtil.SetXmlProperties(obj2, item.ParametersDictionary);
            ReflectionUtil.SetXmlProperties(obj2, item.LocalizedParametersDictionary);
            model3.Parameters.AddRange(item.MvcValidationMessages);
            if (!string.IsNullOrEmpty(item.Conditions))
            {
                RulesManager.RunRules(item.Conditions, model3);
            }
            if (!model3.Visible)
            {
                return null;
            }
            model3.Initialize();
            if (!formViewModel.ReadQueryString || string.IsNullOrEmpty(formViewModel.QueryParameters?[model3.Title]))
                return model3;
            var method = model3.GetType().GetMethod("SetValueFromQuery");
            if (method != null)
            {
                method.Invoke(model3, new object[] { formViewModel.QueryParameters[model3.Title] });
            }
            return model3;
        }

        protected SectionViewModel GetSectionViewModel(SectionItem item, FormViewModel formViewModel)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(formViewModel, "formViewModel");
            var model = new SectionViewModel
            {
                Fields = new List<FieldViewModel>(),
                Item = item.InnerItem
            };
            var title = item.Title;
            model.Visible = true;
            if (!string.IsNullOrEmpty(title))
            {
                model.ShowInformation = true;
                model.Title = item.Title ?? string.Empty;
                ReflectionUtils.SetXmlProperties(model, item.Parameters, true);
                model.ShowTitle = model.ShowLegend != "No";
                ReflectionUtils.SetXmlProperties(model, item.LocalizedParameters, true);
            }
            model.Fields = (from x in item.Fields
                            select GetFieldViewModel(x, formViewModel) into x
                            where x != null
                            select x).ToList<FieldViewModel>();
            if (!string.IsNullOrEmpty(item.Conditions))
            {
                RulesManager.RunRules(item.Conditions, model);
            }
            return model.Visible ? model : null;
        }

        public FormViewModel GetView(FormModel modelEntity)
        {
            Assert.ArgumentNotNull(modelEntity, "modelEntity");
            var formViewModel = new FormViewModel
            {
                UniqueId = modelEntity.UniqueId,
                Information = modelEntity.Item.Introduction ?? string.Empty,
                IsAjaxForm = modelEntity.Item.IsAjaxMvcForm,
                IsSaveFormDataToStorage = modelEntity.Item.IsSaveFormDataToStorage,
                Title = modelEntity.Item.FormName ?? string.Empty,
                TitleTag = modelEntity.Item.TitleTag.ToString(),
                ShowTitle = modelEntity.Item.ShowTitle,
                ShowFooter = modelEntity.Item.ShowFooter,
                ShowInformation = modelEntity.Item.ShowIntroduction,
                SubmitButtonName = modelEntity.Item.SubmitName ?? string.Empty,
                SubmitButtonPosition = modelEntity.Item.SubmitButtonPosition ?? string.Empty,
                SubmitButtonSize = modelEntity.Item.SubmitButtonSize ?? string.Empty,
                SubmitButtonType = modelEntity.Item.SubmitButtonType ?? string.Empty,
                SuccessMessage = modelEntity.Item.SuccessMessage ?? string.Empty,
                SuccessSubmit = false,
                Errors = (from x in modelEntity.Failures select x.ErrorMessage).ToList<string>(),
                Visible = true,
                LeftColumnStyle = modelEntity.Item.LeftColumnStyle,
                RightColumnStyle = modelEntity.Item.RightColumnStyle,
                Footer = modelEntity.Item.Footer,
                Item = modelEntity.Item.InnerItem,
                FormType = modelEntity.Item.FormType,
                ReadQueryString = modelEntity.ReadQueryString,
                QueryParameters = modelEntity.QueryParameters
            };
            var builder = new StringBuilder();
            builder.Append(modelEntity.Item.FormTypeClass ?? string.Empty).Append(" ").Append(modelEntity.Item.CustomCss ?? string.Empty).Append(" ").Append(modelEntity.Item.FormAlignment ?? string.Empty);
            formViewModel.CssClass = builder.ToString().Trim();
            ReflectionUtils.SetXmlProperties(formViewModel, modelEntity.Item.Parameters, true);
            formViewModel.Sections = (from x in modelEntity.Item.SectionItems
                                      select this.GetSectionViewModel(new SectionItem(x), formViewModel) into x
                                      where x != null
                                      select x).ToList<SectionViewModel>();
            return formViewModel;
        }

        public void SetModelResults(FormViewModel view, FormModel formModel)
        {
            Assert.ArgumentNotNull(view, "view");
            Assert.ArgumentNotNull(formModel, "formModel");


            foreach (var section in view.Sections)
            {
                foreach (var field in section.Fields)
                {
                    formModel.Results.Add(((IFieldResult)field).GetResult());
                }
            }

            var results = view.Sections.SelectMany(x => x.Fields).Select(x => ((IFieldResult)x).GetResult()).Where(x => x != null);
            foreach (var result in results)
            {
                if (result.Value == null) result.Value = string.Empty;
            }


            formModel.Results = results.ToList();

            foreach (var formModelResult in formModel.Results)
            {
                if (formModelResult.Value == null) formModelResult.Value = string.Empty;
            }

        }
    }
}
