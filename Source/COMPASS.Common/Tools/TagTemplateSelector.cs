﻿using COMPASS.Common.Models;

namespace COMPASS.Common.Tools
{
    public class TagTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement? element = container as FrameworkElement;
            bool isGroup;
            if (item is TreeViewNode node)
            {
                isGroup = node.Tag.IsGroup;
            }
            else
            {
                CheckableTreeNode<Tag>? checkNode = item as CheckableTreeNode<Tag>;
                isGroup = checkNode?.Item.IsGroup ?? false;
            }

            if (isGroup)
            {
                return element?.FindResource("GroupTag") as HierarchicalDataTemplate;
            }
            else
            {
                return element?.FindResource("RegularTag") as HierarchicalDataTemplate;
            }
        }
    }
}
